using Confluent.Kafka;
using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using FuelStation.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FuelStation.IntegrationTests;

public class FuellingAndDeliveryIntegrationTests : IntegrationTestBase
{
    private const string StationId = "station-1";
    private const string TankId = "tank-1";
    private const string PumpId = "pump-1";
    private const FuelType FuelType = Fuel.FuelType.Ai95;

    public FuellingAndDeliveryIntegrationTests(IntegrationTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task FullFuellingCycle_CreatesAndCompletesSession()
    {
        //# Arrange: clean database and seed test data
        await Fixture.ResetDatabaseAsync();
        await SeedTestStationAsync();

        var idempotencyKey = Guid.NewGuid().ToString();

        //# Act: start fuelling
        var startReply = await Client.StartFuellingAsync(new StartFuellingRequest
        {
            StationId = StationId,
            PumpId = PumpId,
            FuelType = FuelType,
            PreauthorizedLitres = 100,
            IdempotencyKey = idempotencyKey
        });

        //# Assert: start succeeded
        Assert.True(startReply.Success);
        Assert.False(string.IsNullOrEmpty(startReply.SessionId));

        // Check session exists in DB with status Reserved
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.FuellingSessions.FindAsync(startReply.SessionId);
            Assert.NotNull(session);
            Assert.Equal(SessionStatus.Reserved, session.Status);
        }

        //# Act: complete fuelling with actual volume 80
        var completeReply = await Client.CompleteFuellingAsync(new CompleteFuellingRequest
        {
            StationId = StationId,
            SessionId = startReply.SessionId,
            ActualLitres = 80
        });

        //# Assert: complete succeeded
        Assert.True(completeReply.Success);

        // Check tank volume updated: 500 - 100 + (100-80) = 420
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tank = await db.Tanks.FindAsync(TankId);
            Assert.Equal(420, tank!.CurrentVolume);

            var session = await db.FuellingSessions.FindAsync(startReply.SessionId);
            Assert.Null(session); // session should be removed
        }

        // Verify Kafka events were produced
        var messages = await ReadKafkaMessagesAsync(KafkaTopics.FuellingStarted, KafkaTopics.FuellingCompleted);
        Assert.Contains(messages, m => m.Contains(KafkaMessageKeys.StationId));
        Assert.Contains(messages, m => m.Contains(KafkaMessageKeys.SessionId));
        Assert.Contains(messages, m => m.Contains(KafkaMessageKeys.Litres));
    }
    
    [Fact]
    public async Task FullDeliveryCycle_CreatesAndCompletesSession()
    {
        //# Arrange: clean database and seed test data
        await Fixture.ResetDatabaseAsync();
        await SeedTestStationAsync();

        var idempotencyKey = Guid.NewGuid().ToString();

        //# Act: start delivery
        var startReply = await Client.StartDeliveryAsync(new StartDeliveryRequest()
        {
            StationId = StationId,
            Compartments = { new Compartment { FuelType = FuelType, Litres = 300 } },
            IdempotencyKey = idempotencyKey
        });

        //# Assert: start succeeded
        Assert.True(startReply.Success);
        Assert.False(string.IsNullOrEmpty(startReply.SessionId));

        // Check session exists in DB with status Completed
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            DeliverySessionEntity? session = null;

            const int maxRetryCount = 20;
            for (var i = 0; i < maxRetryCount; i++)
            {
                await Task.Delay(300);
                session = await db.DeliverySessions.FindAsync(startReply.SessionId);

                if (session != null && session.Status == DeliverySessionStatus.Completed)
                    break;
            }

            Assert.NotNull(session);
            Assert.Equal(DeliverySessionStatus.Completed, session.Status);
        }

        // Check tank volume updated: 500 - 100 + (100-80) = 420
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tank = await db.Tanks.FindAsync(TankId);
            Assert.Equal(800, tank!.CurrentVolume);
        }
        
        //# Act
        var startReplyRepeating = await Client.StartDeliveryAsync(new StartDeliveryRequest()
        {
            StationId = StationId,
            Compartments = { new Compartment { FuelType = FuelType, Litres = 100 } },
            IdempotencyKey = idempotencyKey
        });
        
        Assert.Equal(startReply.SessionId, startReplyRepeating.SessionId);
        
        // Check session exists in DB with status Reserved
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            const int maxRetryCount = 20;
            for (var i = 0; i < maxRetryCount; i++)
            {
                await Task.Delay(300);
                var session = await db.DeliverySessions.FindAsync(startReply.SessionId);

                if (session != null && session.Status == DeliverySessionStatus.Completed)
                    break;
            }
            
            var sessions = await db.DeliverySessions.ToListAsync();
            Assert.Single(sessions); // only one session
            Assert.Equal(startReply.SessionId, sessions[0].Id);
        }

        // Verify Kafka events were produced
        var messages = await ReadKafkaMessagesAsync(KafkaTopics.DeliveryEvents);
        Assert.Contains(messages, m => m.Contains(KafkaMessageKeys.StationId));
        Assert.Contains(messages, m => m.Contains(KafkaMessageKeys.SessionId));
        Assert.Contains(messages, m => m.Contains(KafkaMessageKeys.DeliveryStatus));
    }

    private async Task SeedTestStationAsync()
    {
        using var scope = Fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var station = new StationEntity { Id = StationId, Name = "Test Station", Address = "Nowhere" };
        var tank = new TankEntity
        {
            Id = TankId,
            FuelType = FuelType,
            Capacity = 1000,
            CurrentVolume = 500,
            StationId = station.Id
        };
        var pump = new PumpEntity { Id = PumpId, StationId = station.Id };
        var nozzle = new NozzleEntity
        {
            Id = Guid.NewGuid().ToString(),
            FuelType = FuelType,
            TankId = tank.Id,
            PumpId = pump.Id
        };
        pump.Nozzles.Add(nozzle);

        db.Stations.Add(station);
        db.Tanks.Add(tank);
        db.Pumps.Add(pump);
        db.Nozzles.Add(nozzle);
        await db.SaveChangesAsync();
    }

    private async Task<List<string>> ReadKafkaMessagesAsync(params string[] topics)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = Fixture.KafkaBootstrapServers,
            GroupId = $"test-consumer-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        var messages = new List<string>();
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topics);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(500));
            if (consumeResult?.Message != null)
            {
                messages.Add(consumeResult.Message.Value);
            }
        }
        consumer.Close();
        return messages;
    }
}