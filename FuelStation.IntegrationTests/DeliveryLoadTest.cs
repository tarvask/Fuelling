using Fuel;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FuelStation.IntegrationTests;

public class DeliveryLoadTest : IntegrationTestBase
{
    private const string Station1Id = "station-1";
    private const string Station2Id = "station-2";
    private const string Tank1Id = "tank-1";
    private const string Tank2Id = "tank-2";
    private const string Pump1Id = "pump-1";
    private const string Pump2Id = "pump-2";
    private const FuelType FuelType1 = FuelType.Ai95;
    private const FuelType FuelType2 = FuelType.Dt;
    private const double Tank1InitialVolume = 0;
    private const double Tank2InitialVolume = 1000;
    private const double DeliveryAmount = 1000;

    public DeliveryLoadTest(IntegrationTestFixture fixture) : base(fixture) { }
    
    [Fact]
    public async Task ConcurrentDelivery_ShouldNotExceedTankCapacity()
    {
        //# Arrange
        await Fixture.ResetDatabaseAsync();
        await SeedTestTwoStationsWithSingleTanksAsync();

        //# Act
        // launch parallelFuellingSessionsCount processes
        var task1 = Client.StartDeliveryAsync(new StartDeliveryRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = Station1Id,
            Compartments = { new Compartment { FuelType = FuelType1, Litres = DeliveryAmount } }
        }).ResponseAsync;
        
        var task2 = Client.StartDeliveryAsync(new StartDeliveryRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = Station2Id,
            Compartments = { new Compartment { FuelType = FuelType2, Litres = DeliveryAmount } }
        }).ResponseAsync;
        
        var responses = await Task.WhenAll(task1, task2);

        //# Assert
        Assert.All(responses, r => Assert.True(r.Success));

        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            DeliverySessionEntity? session1 = null;
            DeliverySessionEntity? session2 = null;

            const int maxRetryCount = 100;
            for (var i = 0; i < maxRetryCount; i++)
            {
                await Task.Delay(300);
                // reload from db
                session1 = await db.DeliverySessions.FindAsync(responses[0].SessionId);
                session2 = await db.DeliverySessions.FindAsync(responses[1].SessionId);

                // for already tracked items FindAsync returns cache,
                // so force reloading
                if (session1 != null) await db.Entry(session1).ReloadAsync();
                if (session2 != null) await db.Entry(session2).ReloadAsync();

                if (session1 != null && (session1.Status == DeliverySessionStatus.Completed || session1.Status == DeliverySessionStatus.Failed)
                    && session2 != null && (session2.Status == DeliverySessionStatus.Completed || session2.Status == DeliverySessionStatus.Failed))
                    break;
            }
        }

        using (var finalScope = Fixture.Factory.Services.CreateScope())
        {
            var db = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
            DeliverySessionEntity? session1 = await db.DeliverySessions.FindAsync(responses[0].SessionId);
            DeliverySessionEntity? session2 = await db.DeliverySessions.FindAsync(responses[1].SessionId);

            Assert.NotNull(session1);
            Assert.Equal(DeliverySessionStatus.Completed, session1.Status);
            Assert.NotNull(session2);
            Assert.Equal(DeliverySessionStatus.Completed, session2.Status);

            // fuel unloaded successfully
            var tank1 = await db.Tanks.FindAsync(Tank1Id);
            Assert.Equal((decimal)(Tank1InitialVolume + DeliveryAmount), tank1!.CurrentVolume);
            var tank2 = await db.Tanks.FindAsync(Tank2Id);
            Assert.Equal((decimal)(Tank2InitialVolume + DeliveryAmount), tank2!.CurrentVolume);
            // all sessions created successfully
            var sessions = await db.DeliverySessions.ToListAsync();
            Assert.Equal(2, sessions.Count);
        }
    }

    private async Task SeedTestTwoStationsWithSingleTanksAsync()
    {
        using var scope = Fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var station1 = new StationEntity { Id = Station1Id, Name = "Load Test 1", Address = "Nowhere1" };
        var station2 = new StationEntity { Id = Station2Id, Name = "Load Test 2", Address = "Nowhere2" };
        var tank1 = new TankEntity { Id = Tank1Id, FuelType = FuelType1, Capacity = 1000, CurrentVolume = (decimal)Tank1InitialVolume, StationId = station1.Id };
        var tank2 = new TankEntity { Id = Tank2Id, FuelType = FuelType2, Capacity = 2000, CurrentVolume = (decimal)Tank2InitialVolume, StationId = station2.Id };

        db.Stations.Add(station1);
        db.Stations.Add(station2);
        db.Tanks.Add(tank1);
        db.Tanks.Add(tank2);

        CreatePump(db, station1.Id, tank1.Id, Pump1Id, FuelType1);
        CreatePump(db, station2.Id, tank2.Id, Pump2Id, FuelType2);
        
        await db.SaveChangesAsync();
    }
}