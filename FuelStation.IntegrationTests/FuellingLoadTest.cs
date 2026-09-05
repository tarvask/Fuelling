using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FuelStation.IntegrationTests;

public class FuellingLoadTest : IntegrationTestBase
{
    private const string StationId = "station-1";
    private const string TankId = "tank-1";
    private const string Pump1Id = "pump-1";
    private const string Pump2Id = "pump-2";
    private const FuelType FuelType = Fuel.FuelType.Ai95;
    private const double TankInitialVolume = 1000;
    
    public FuellingLoadTest(IntegrationTestFixture fixture) : base(fixture) { }
    
    [Fact]
    public async Task ConcurrentFuelling_ShouldNotExceedTankCapacity()
    {
        //# Arrange
        await Fixture.ResetDatabaseAsync();
        
        const int parallelFuellingSessionsCount = 5;
        const double litresEach = 200;
        
        await SeedTestStationWithManyPumpsAsync(parallelFuellingSessionsCount);
        var idempotencyKeys = Enumerable.Range(0, parallelFuellingSessionsCount)
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();

        var tasks = new Task<StartFuellingResponse>[parallelFuellingSessionsCount];

        //# Act
        // launch parallelFuellingSessionsCount processes
        for (int i = 0; i < parallelFuellingSessionsCount; i++)
        {
            tasks[i] = Client.StartFuellingAsync(new StartFuellingRequest
            {
                IdempotencyKey = idempotencyKeys[i],
                StationId = StationId,
                PumpId = string.Empty, // auto-select
                FuelType = FuelType.Ai95,
                PreauthorizedLitres = litresEach
            }).ResponseAsync;
        }

        var responses = await Task.WhenAll(tasks);

        //# Assert
        Assert.All(responses, r => Assert.True(r.Success));

        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tank = await db.Tanks.FindAsync(TankId);
            // is empty
            Assert.Equal((decimal)(TankInitialVolume - litresEach * parallelFuellingSessionsCount), tank!.CurrentVolume);
            // all sessions created successfully
            var sessions = await db.FuellingSessions.ToListAsync();
            Assert.Equal(parallelFuellingSessionsCount, sessions.Count);

            foreach (var session in sessions)
            {
                Assert.Equal(SessionStatus.Reserved, session.Status);
            }
        }
    }
    
    [Fact]
    public async Task PumpBusy_WhenExplicitPumpId_WithSharedTank()
    {
        //# Arrange
        await Fixture.ResetDatabaseAsync();
        
        const double litresEach = 200;
        
        await SeedTestStationWithSharedTankAsync();

        //# Act
        var task1 = Client.StartFuellingAsync(new StartFuellingRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = StationId,
            PumpId = Pump1Id,
            FuelType = FuelType.Ai95,
            PreauthorizedLitres = litresEach
        }).ResponseAsync;
        
        var task2 = Client.StartFuellingAsync(new StartFuellingRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = StationId,
            PumpId = Pump2Id,
            FuelType = FuelType.Ai95,
            PreauthorizedLitres = litresEach
        }).ResponseAsync;

        var responses = await Task.WhenAll(task1, task2);
        
        var task3 = Client.StartFuellingAsync(new StartFuellingRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = StationId,
            PumpId = Pump2Id,
            FuelType = FuelType.Ai95,
            PreauthorizedLitres = litresEach
        }).ResponseAsync;

        var task3Result = await task3;

        //# Assert
        Assert.True(responses[0].Success);
        Assert.True(responses[1].Success);
        Assert.False(task3Result.Success);
        Assert.Contains(string.Format(ErrorMessages.PumpIsBusy, Pump2Id), task3Result.Error);
    }
    
    [Fact]
    public async Task PumpNotAutoSelected_WhenAllPumpsBusy()
    {
        //# Arrange
        await Fixture.ResetDatabaseAsync();
        
        const double litresEach = 200;
        
        await SeedTestStationWithSharedTankAsync();

        //# Act
        var task1 = Client.StartFuellingAsync(new StartFuellingRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = StationId,
            PumpId = Pump1Id,
            FuelType = FuelType.Ai95,
            PreauthorizedLitres = litresEach
        }).ResponseAsync;
        
        var task2 = Client.StartFuellingAsync(new StartFuellingRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = StationId,
            PumpId = Pump2Id,
            FuelType = FuelType.Ai95,
            PreauthorizedLitres = litresEach
        }).ResponseAsync;

        var responses = await Task.WhenAll(task1, task2);
        
        var task3 = Client.StartFuellingAsync(new StartFuellingRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = StationId,
            PumpId = string.Empty, // auto-select
            FuelType = FuelType.Ai95,
            PreauthorizedLitres = litresEach
        }).ResponseAsync;

        var task3Result = await task3;

        //# Assert
        Assert.True(responses[0].Success);
        Assert.True(responses[1].Success);
        Assert.False(task3Result.Success);
        Assert.Contains(string.Format(ErrorMessages.PumpNotAutoSelected, FuelType), task3Result.Error);
    }
    
    [Fact]
    public async Task TankBusy_WhenTankLockNotAcquired()
    {
        //# Arrange
        await Fixture.ResetDatabaseAsync();
        
        const double litresEach = 200;
        
        await SeedTestStationWithSingleTankAsync();

        //# Act
        // lock tank in Redis manually
        var lockProvider = Fixture.Factory.Services.GetRequiredService<IRedisLockProvider>();
        var lockToken = await lockProvider.TryAcquireLockAsync(LockConstants.TankLockKey(TankId), TimeSpan.FromSeconds(10));
        Assert.NotNull(lockToken);
        
        var startFuellingResponse = await Client.StartFuellingAsync(new StartFuellingRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            StationId = StationId,
            PumpId = Pump1Id,
            FuelType = FuelType.Ai95,
            PreauthorizedLitres = litresEach
        }).ResponseAsync;

        //# Assert
        Assert.False(startFuellingResponse.Success);
        Assert.Contains(string.Format(ErrorMessages.TankIsBusy, TankId), startFuellingResponse.Error);
    }
    
    private async Task SeedTestStationWithManyPumpsAsync(int pumpsCount)
    {
        using var scope = Fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var station = new StationEntity { Id = StationId, Name = "Load Test", Address = "Nowhere" };
        var tank = new TankEntity { Id = TankId, FuelType = FuelType.Ai95, Capacity = 1000, CurrentVolume = (decimal)TankInitialVolume, StationId = station.Id };

        db.Stations.Add(station);
        db.Tanks.Add(tank);

        for (int i = 1; i <= pumpsCount; i++)
        {
            CreatePump(db, station.Id, tank.Id, $"pump-{i}", FuelType);
        }

        await db.SaveChangesAsync();
    }
    
    private async Task SeedTestStationWithSharedTankAsync()
    {
        using var scope = Fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var station = new StationEntity { Id = StationId, Name = "Load Test", Address = "Nowhere" };
        var tank = new TankEntity { Id = TankId, FuelType = FuelType.Ai95, Capacity = 1000, CurrentVolume = (decimal)TankInitialVolume, StationId = station.Id };

        db.Stations.Add(station);
        db.Tanks.Add(tank);

        CreatePump(db, StationId, TankId, Pump1Id, FuelType);
        CreatePump(db, StationId, TankId, Pump2Id, FuelType);

        await db.SaveChangesAsync();
    }
    
    private async Task SeedTestStationWithSingleTankAsync()
    {
        using var scope = Fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var station = new StationEntity { Id = StationId, Name = "Load Test", Address = "Nowhere" };
        var tank = new TankEntity { Id = TankId, FuelType = FuelType.Ai95, Capacity = 1000, CurrentVolume = (decimal)TankInitialVolume, StationId = station.Id };

        db.Stations.Add(station);
        db.Tanks.Add(tank);

        CreatePump(db, StationId, TankId, Pump1Id, FuelType);

        await db.SaveChangesAsync();
    }
}