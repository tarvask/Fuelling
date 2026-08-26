using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using FuelStation.ReservationService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit.Abstractions;

namespace FuelStation.Tests;

public class DeliveryOrchestratorTests
{
    private readonly ITestOutputHelper _output;
    
    public DeliveryOrchestratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task StartDeliveryProcess_ValidRequest_ReturnsSuccessAndCreatesScheduledSession()
    {
        //# Arrange
        // base
        var simulationConfig = TestHelpers.CreateTestSimulationConfig(1, 1);
        var (orchestrator, serviceProvider, scopeFactory, _, _, kafka) = CreateOrchestratorWithInMemoryDb(simulationConfig);
        var (stationId, _, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        var compartments = new List<Compartment> { new() { FuelType = FuelType.Ai95, Litres = 1000 } };
        
        //# Act
        var result = await orchestrator.StartDeliveryProcessAsync(stationId, compartments, Guid.NewGuid().ToString());

        //# Assert
        Assert.True(result.Success);
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.DeliverySessions.FirstOrDefaultAsync(s => s.Id == result.SessionId);
            Assert.NotNull(session);
            Assert.Equal(DeliverySessionStatus.Scheduled, session.Status);
        }
        await kafka.Received(1).SendDeliveryEvent(stationId, result.SessionId!, DeliverySessionStatus.Scheduled.ToString());
    }

    [Fact]
    public async Task StartDelivery_WithZeroDelays_CompletesDelivery()
    {
        //# Arrange
        // base
        var simulationConfig = TestHelpers.CreateTestSimulationConfig();
        var (orchestrator, serviceProvider, scopeFactory, _, _, kafka) = CreateOrchestratorWithInMemoryDb(simulationConfig);
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        using (var arrangeScope = scopeFactory.CreateScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tank = await db.Tanks.FindAsync(tankId);
            tank!.Capacity = 2000;
            await db.SaveChangesAsync();
        }
        
        var compartments = new List<Compartment> { new() { FuelType = FuelType.Ai95, Litres = 1000 } };

        //# Act
        var startResult = await orchestrator.StartDeliveryProcessAsync(stationId, compartments, Guid.NewGuid().ToString());
        var status = await WaitForDeliverySessionComplete(serviceProvider, startResult.SessionId!);

        //# Assert
        Assert.Equal(DeliverySessionStatus.Completed, status);
        await kafka.Received(1).SendDeliveryEvent(stationId, startResult.SessionId!, DeliverySessionStatus.Scheduled.ToString());
        await kafka.Received(1).SendDeliveryEvent(stationId, startResult.SessionId!, DeliverySessionStatus.Arrived.ToString());
        await kafka.Received(1).SendDeliveryEvent(stationId, startResult.SessionId!, DeliverySessionStatus.Completed.ToString());
        using (var assertScope = serviceProvider.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100 + (int)compartments[0].Litres, tank!.CurrentVolume);
        }
    }

    [Fact]
    public async Task StartDelivery_MultipleCompartments_FillsCorrectTanks()
    {
        //# Arrange
        // base
        var simulationConfig = TestHelpers.CreateTestSimulationConfig();
        var (orchestrator, serviceProvider, scopeFactory, _, _, kafka) = CreateOrchestratorWithInMemoryDb(simulationConfig);

        string stationId = "station-multi";
        string tankAi92Id = "tank-ai92";
        string tankAi95Id = "tank-ai95";
        string tankDtId = "tank-dt";
        const int capacity = 200;
        const int ai92CurrentVolume = 50;
        const int ai95CurrentVolume = 60;
        const int dtCurrentVolume = 70;

        using (var seedScope = scopeFactory.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var station = new StationEntity { Id = stationId, Name = "Multi Fuel", Address = "Nowhere" };
            db.Stations.Add(station);

            db.Tanks.AddRange(
                new TankEntity { Id = tankAi92Id, FuelType = FuelType.Ai92, Capacity = capacity, CurrentVolume = ai92CurrentVolume, StationId = stationId },
                new TankEntity { Id = tankAi95Id, FuelType = FuelType.Ai95, Capacity = capacity, CurrentVolume = ai95CurrentVolume, StationId = stationId },
                new TankEntity { Id = tankDtId,   FuelType = FuelType.Dt,   Capacity = capacity, CurrentVolume = dtCurrentVolume, StationId = stationId }
            );
            await db.SaveChangesAsync();
        }
        
        var compartments = new List<Compartment>
        {
            new() { FuelType = FuelType.Ai92, Litres = 50 },
            new() { FuelType = FuelType.Ai95, Litres = 100 },
            new() { FuelType = FuelType.Dt,   Litres = 150 }
        };
        
        var startResult = await orchestrator.StartDeliveryProcessAsync(stationId, compartments, Guid.NewGuid().ToString());
        var status = await WaitForDeliverySessionComplete(serviceProvider, startResult.SessionId!);
        
        //# Assert
        Assert.Equal(DeliverySessionStatus.Completed, status);
        using (var assertScope = serviceProvider.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tank92 = await db.Tanks.FindAsync(tankAi92Id);
            Assert.Equal(ai92CurrentVolume + (int)compartments[0].Litres, tank92!.CurrentVolume);
            var tank95 = await db.Tanks.FindAsync(tankAi95Id);
            Assert.Equal(ai95CurrentVolume + (int)compartments[1].Litres, tank95!.CurrentVolume);
            var tankDt = await db.Tanks.FindAsync(tankDtId);
            Assert.Equal(int.Clamp(dtCurrentVolume + (int)compartments[2].Litres, 0, capacity), tankDt!.CurrentVolume);
        }
    }

    [Fact]
    public async Task StartDelivery_NonExistentStation_ReturnsError()
    {
        //# Arrange
        // base
        var simulationConfig = TestHelpers.CreateTestSimulationConfig();
        var (orchestrator, serviceProvider, scopeFactory, _, _, _) = CreateOrchestratorWithInMemoryDb(simulationConfig);
        var (_, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        const string wrongStationId = "missing-station";
        var compartments = new List<Compartment> { new() { FuelType = FuelType.Ai95, Litres = 1000 } };
        
        //# Act
        var result = await orchestrator.StartDeliveryProcessAsync(wrongStationId, compartments, Guid.NewGuid().ToString());
        
        //# Assert
        Assert.False(result.Success);
        Assert.Contains(string.Format(ErrorMessages.StationNotFound, wrongStationId), result.Error);
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.DeliverySessions.FirstOrDefaultAsync());
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, tank!.CurrentVolume);
        }
    }

    [Fact]
    public async Task StartDelivery_StationClosedDeliveryRejected_ReturnsError()
    {
        //# Arrange
        // base
        var simulationConfig = TestHelpers.CreateTestSimulationConfig();
        var (orchestrator, serviceProvider, scopeFactory, lockProvider, _, kafka) = CreateOrchestratorWithInMemoryDb(simulationConfig);
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        // lock station
        lockProvider.TryAcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Returns((RedisLockToken?)null);
        lockProvider.IsLockedAsync(Arg.Any<string>()).Returns(true);
        
        var compartments = new List<Compartment> { new() { FuelType = FuelType.Ai95, Litres = 1000 } };
        
        //# Act
        var startResult = await orchestrator.StartDeliveryProcessAsync(stationId, compartments, Guid.NewGuid().ToString());
        var status = await WaitForDeliverySessionComplete(serviceProvider, startResult.SessionId!);
        
        //# Assert
        Assert.True(startResult.Success);
        Assert.Equal(DeliverySessionStatus.Failed, status);
        await kafka.Received(1).SendDeliveryEvent(stationId, startResult.SessionId!, DeliverySessionStatus.Scheduled.ToString());
        await kafka.Received(1).SendDeliveryEvent(stationId, startResult.SessionId!, DeliverySessionStatus.Failed.ToString());
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull(await db.DeliverySessions.FindAsync(startResult.SessionId));
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, tank!.CurrentVolume);
        }
    }
    
    [Fact]
    public async Task StartDelivery_TankIsBusy_ReturnsError()
    {
        //# Arrange
        // base
        var simulationConfig = TestHelpers.CreateTestSimulationConfig();
        var (orchestrator, serviceProvider, scopeFactory, lockProvider, _, kafka) = CreateOrchestratorWithInMemoryDb(simulationConfig);
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        // lock station
        var lockKey = LockConstants.TankLockKey(tankId);
        lockProvider.TryAcquireLockAsync(lockKey, Arg.Any<TimeSpan>())
            .Returns((RedisLockToken?)null);
        lockProvider.IsLockedAsync(lockKey).Returns(true);
        
        var compartments = new List<Compartment> { new() { FuelType = FuelType.Ai95, Litres = 1000 } };
        
        //# Act
        var startResult = await orchestrator.StartDeliveryProcessAsync(stationId, compartments, Guid.NewGuid().ToString());
        var status = await WaitForDeliverySessionComplete(serviceProvider, startResult.SessionId!);

        //# Assert
        Assert.True(startResult.Success);
        Assert.Equal(DeliverySessionStatus.Failed, status);
        await kafka.Received(1).SendDeliveryEvent(stationId, startResult.SessionId!, DeliverySessionStatus.Scheduled.ToString());
        await kafka.Received(1).SendDeliveryEvent(stationId, startResult.SessionId!, DeliverySessionStatus.Failed.ToString());
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull(await db.DeliverySessions.FindAsync(startResult.SessionId));
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, tank!.CurrentVolume);
        }
    }

    [Fact]
    public async Task StartDelivery_NoIdempotencyKey_ReturnsError()
    {
        //# Arrange
        // base
        var simulationConfig = TestHelpers.CreateTestSimulationConfig();
        var (orchestrator, serviceProvider, scopeFactory, _, _, _) = CreateOrchestratorWithInMemoryDb(simulationConfig);
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        var compartments = new List<Compartment> { new() { FuelType = FuelType.Ai95, Litres = 1000 } };
        
        //# Act
        const string emptyIdempotencyKey = "";
        var result = await orchestrator.StartDeliveryProcessAsync(stationId, compartments, emptyIdempotencyKey);
        
        //# Assert
        Assert.False(result.Success);
        Assert.Contains(string.Format(ErrorMessages.IdempotencyKeyNotProvidedForDelivering), result.Error);
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.DeliverySessions.FirstOrDefaultAsync());
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, tank!.CurrentVolume);
        }
    }
    
    [Fact]
    public async Task StartDelivery_RepeatedRequestWithSameIdempotencyKey_ReturnsCachedResult()
    {
        //# Arrange
        // base
        var simulationConfig = TestHelpers.CreateTestSimulationConfig();
        var (orchestrator, serviceProvider, scopeFactory, _, idempotencyProvider, kafka) = CreateOrchestratorWithInMemoryDb(simulationConfig);
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        var compartments = new List<Compartment> { new() { FuelType = FuelType.Ai95, Litres = 1000 } };

        const string repeatingIdempotencyKey = "repeated-key";
        const string sessionId = "existing-session";
        var cachedResult = StartDeliveryResult.Ok(sessionId);
        idempotencyProvider.GetIdempotencyResultAsync<StartDeliveryResult>(repeatingIdempotencyKey)
            .Returns(cachedResult);

        //# Act
        var repeatedResult = await orchestrator.StartDeliveryProcessAsync(stationId, compartments, repeatingIdempotencyKey);

        //# Assert
        Assert.True(repeatedResult.Success);
        Assert.Equal(sessionId, repeatedResult.SessionId);

        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.DeliverySessions.FirstOrDefaultAsync());
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, tank!.CurrentVolume);
        }
        
        await kafka.DidNotReceive().SendDeliveryEvent(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
    
    private (DeliveryOrchestrator orchestrator, ServiceProvider serviceProvider, IServiceScopeFactory scopeFactory,
        IRedisLockProvider lockProvider,
        IRedisIdempotencyProvider idempotencyProvider,
        IKafkaProducerService kafka
        ) CreateOrchestratorWithInMemoryDb(IOptions<SimulationConfig> simulationConfig)
    {
        var (serviceProvider, scopeFactory, lockProvider, idempotencyProvider, kafka) = TestHelpers.CreateServiceProviderWithMocks(simulationConfig);
        var orchestrator = new DeliveryOrchestrator(scopeFactory, lockProvider, idempotencyProvider, kafka,
            serviceProvider.GetRequiredService<IOptions<SimulationConfig>>(),
            Substitute.For<ILogger<DeliveryOrchestrator>>());
        return (orchestrator, serviceProvider, scopeFactory, lockProvider, idempotencyProvider, kafka);
    }
    
    private async Task<DeliverySessionStatus?> WaitForDeliverySessionComplete(ServiceProvider serviceProvider, string sessionId)
    {
        // wait to 5 seconds, until the session becomes Completed
        using var actScope = serviceProvider.CreateScope();
        var db = actScope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (int i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            var session = await db.DeliverySessions.FindAsync(sessionId);
            if (session != null && session.Status is DeliverySessionStatus.Completed or DeliverySessionStatus.Failed)
                return session.Status;
        }

        return null;
    }
}