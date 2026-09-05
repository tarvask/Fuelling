using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FuelStation.IntegrationTests;

public class DeliveryConflictTest : IntegrationTestBase
{
    private const string Station1Id = "station-1";
    private const string Tank1Id = "tank-1";
    private const string Pump1Id = "pump-1";
    private const FuelType FuelType1 = FuelType.Ai95;
    private const double Tank1InitialVolume = 0;
    private const double DeliveryAmount = 1000;
    
    public DeliveryConflictTest(IntegrationTestFixture fixture) : base(fixture) { }
    
    [Fact]
    public async Task StationClosedDeliveryRejected_WhenTwoDeliveriesSameStation()
    {
        //# Arrange
        await Fixture.ResetDatabaseAsync();
        await SeedTestStationWithSingleTankAsync();

        //# Act
        // launch the first delivery ahead of the second one
        var task1Result = await Client.StartDeliveryAsync(new StartDeliveryRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = Station1Id,
            Compartments = { new Compartment { FuelType = FuelType1, Litres = DeliveryAmount } }
        });
        Assert.True(task1Result.Success);
        
        const int maxRetryCount = 20;
        DeliverySessionEntity? session1 = null;

        // wait for first delivery to capture the station (status Arrived)
        using (var task1Scope = Fixture.Factory.Services.CreateScope())
        {
            var task1db = task1Scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            for (var i = 0; i < maxRetryCount; i++)
            {
                await Task.Delay(300);
                session1 = await task1db.DeliverySessions.FindAsync(task1Result.SessionId);

                if (session1 != null && session1.Status == DeliverySessionStatus.Arrived)
                    break;
            }

            Assert.NotNull(session1);
            Assert.Equal(DeliverySessionStatus.Arrived, session1.Status);
        }
        
        var task2Result = await Client.StartDeliveryAsync(new StartDeliveryRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = Station1Id,
            Compartments = { new Compartment { FuelType = FuelType1, Litres = DeliveryAmount } }
        });

        //# Assert
        Assert.True(task2Result.Success);
        DeliverySessionEntity? session2 = null;

        // wait for finishing of both sessions
        using (var task2Scope = Fixture.Factory.Services.CreateScope())
        {
            var task2db = task2Scope.ServiceProvider.GetRequiredService<AppDbContext>();

            for (var i = 0; i < maxRetryCount; i++)
            {
                await Task.Delay(300);
                // reload from db
                session1 = await task2db.DeliverySessions.FindAsync(task1Result.SessionId);
                session2 = await task2db.DeliverySessions.FindAsync(task2Result.SessionId);

                // for already tracked items FindAsync returns cache,
                // so force reloading
                if (session1 != null) await task2db.Entry(session1).ReloadAsync();
                if (session2 != null) await task2db.Entry(session2).ReloadAsync();

                if (session1 != null && session1.Status == DeliverySessionStatus.Completed
                    && session2 != null && session2.Status == DeliverySessionStatus.Failed)
                    break;
            }
        }

        using (var finalScope = Fixture.Factory.Services.CreateScope())
        {
            var db = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
            session1 = await db.DeliverySessions.FindAsync(task1Result.SessionId);
            session2 = await db.DeliverySessions.FindAsync(task2Result.SessionId);
            
            Assert.NotNull(session1);
            Assert.Equal(DeliverySessionStatus.Completed, session1.Status);
            Assert.NotNull(session2);
            Assert.Equal(DeliverySessionStatus.Failed, session2.Status);
            
            // fuel unloaded successfully
            var tank = await db.Tanks.FindAsync(Tank1Id);
            Assert.Equal((decimal)(Tank1InitialVolume + DeliveryAmount), tank!.CurrentVolume);
            // all sessions created successfully
            var sessions = await db.DeliverySessions.ToListAsync();
            Assert.Equal(2, sessions.Count);
        }
    }
    
    [Fact]
    public async Task DeliveryFailed_OnUnloadingException()
    {
        //# Arrange
        await Fixture.ResetDatabaseAsync();
        await SeedTestStationWithSingleTankAsync();
        
        //# Act
        // lock tank in Redis manually
        var lockProvider = Fixture.Factory.Services.GetRequiredService<IRedisLockProvider>();
        var lockToken = await lockProvider.TryAcquireLockAsync(LockConstants.TankLockKey(Tank1Id), TimeSpan.FromSeconds(30));
        Assert.NotNull(lockToken);
        
        var taskResult = await Client.StartDeliveryAsync(new StartDeliveryRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = Station1Id,
            Compartments = { new Compartment { FuelType = FuelType1, Litres = DeliveryAmount } }
        });
        Assert.True(taskResult.Success);
        
        const int maxRetryCount = 30;
        DeliverySessionEntity? session;

        // wait for delivery to Fail
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            for (var i = 0; i < maxRetryCount; i++)
            {
                await Task.Delay(300);
                // reload from db
                session = await db.DeliverySessions.FindAsync(taskResult.SessionId);

                // for already tracked items FindAsync returns cache,
                // so force reloading
                if (session != null) await db.Entry(session).ReloadAsync();

                if (session != null && session.Status == DeliverySessionStatus.Failed)
                    break;
            }
        }

        using (var finalScope = Fixture.Factory.Services.CreateScope())
        {
            var db = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
            session = await db.DeliverySessions.FindAsync(taskResult.SessionId);
            Assert.NotNull(session);
            Assert.Equal(DeliverySessionStatus.Failed, session.Status);
            
            // fuel was not unloaded
            var tank = await db.Tanks.FindAsync(Tank1Id);
            Assert.Equal((decimal)(Tank1InitialVolume), tank!.CurrentVolume);
            // all sessions created successfully
            var sessionsCount = await db.DeliverySessions.CountAsync();
            Assert.Equal(1, sessionsCount);
        }
    }
    
    private async Task SeedTestStationWithSingleTankAsync()
    {
        using var scope = Fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var station = new StationEntity { Id = Station1Id, Name = "Load Test 1", Address = "Nowhere" };
        var tank = new TankEntity { Id = Tank1Id, FuelType = FuelType1, Capacity = 1000, CurrentVolume = (decimal)Tank1InitialVolume, StationId = station.Id };

        db.Stations.Add(station);
        db.Tanks.Add(tank);

        CreatePump(db, station.Id, tank.Id, Pump1Id, FuelType1);
        
        await db.SaveChangesAsync();
    }
}