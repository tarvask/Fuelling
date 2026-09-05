using System.Text.Json;
using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using FuelStation.ReservationService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FuelStation.Tests;

public class ReservationManagerTests
{
    #region StartFuelling
    [Fact]
    public async Task StartFuelling_ValidRequest_ReturnsSuccess()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, pumpId, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        //# Act
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: null,                 // auto-select
            fuelType: FuelType.Ai95,
            preauthorizedLitres: volume,
            idempotencyKey: Guid.NewGuid().ToString()
        );

        //# Assert
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            Assert.True(result.Success, result.Error);
            Assert.False(string.IsNullOrEmpty(result.SessionId));

            var updatedTank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100 - volume, updatedTank!.CurrentVolume);

            var session = await db.FuellingSessions.FirstOrDefaultAsync();
            Assert.NotNull(session);
            Assert.Equal(stationId, session.StationId);
            Assert.Equal(tankId, session.TankId);
            Assert.Equal(pumpId, session.PumpId);
            Assert.Equal(fuelType, session.FuelType);
            Assert.Equal(volume, session.ReservedVolume);
        }
    }

    [Fact]
    public async Task StartFuelling_NoFuelAvailablePumpAutoSelect_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        // set tank empty
        using (var arrangeScope = scopeFactory.CreateScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tank = await db.Tanks.FindAsync(tankId);
            tank!.CurrentVolume = 0;
            await db.SaveChangesAsync();
        }
        
        //# Act
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: null,                 // auto-select
            fuelType: fuelType,
            preauthorizedLitres: volume,
            idempotencyKey: Guid.NewGuid().ToString()
        );
        
        //# Assert
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            Assert.False(result.Success, result.Error);
            Assert.True(string.IsNullOrEmpty(result.SessionId));

            var updatedTank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(0, updatedTank!.CurrentVolume); // no changes

            var session = await db.FuellingSessions.FirstOrDefaultAsync();
            Assert.Null(session);
            Assert.Contains(string.Format(ErrorMessages.PumpNotAutoSelected, fuelType), result.Error);
        }
    }
    
    [Fact]
    public async Task StartFuelling_NoFuelAvailablePumpManualSelect_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, pumpId, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        // set tank empty
        using (var arrangeScope = scopeFactory.CreateScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tank = await db.Tanks.FindAsync(tankId);
            tank!.CurrentVolume = 0;
            await db.SaveChangesAsync();
        }
        
        //# Act
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: pumpId,
            fuelType: fuelType,
            preauthorizedLitres: volume,
            idempotencyKey: Guid.NewGuid().ToString()
        );
        
        //# Assert
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            Assert.False(result.Success, result.Error);
            Assert.True(string.IsNullOrEmpty(result.SessionId));

            var updatedTank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(0, updatedTank!.CurrentVolume); // no changes

            var session = await db.FuellingSessions.FirstOrDefaultAsync();
            Assert.Null(session);
            Assert.Contains(string.Format(ErrorMessages.NoFuelAvailable, tankId), result.Error);
        }
    }
    
    [Fact]
    public async Task StartFuelling_NoFreePumpAutoSelect_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, pumpId, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        // set pump locked
        var redisLockMock = serviceProvider.GetRequiredService<IRedisLockProvider>();
        redisLockMock.IsLockedAsync(pumpId).Returns(true);
        redisLockMock.TryAcquireLockAsync(LockConstants.PumpLockKey(pumpId), Arg.Any<TimeSpan>()).Returns((RedisLockToken?)null);
        redisLockMock.TryAcquireLockWithRetryAsync(LockConstants.PumpLockKey(pumpId),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns((RedisLockToken?)null);
        
        //# Act
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: null,
            fuelType: fuelType,
            preauthorizedLitres: volume,
            idempotencyKey: Guid.NewGuid().ToString()
        );
        
        //# Assert
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            Assert.False(result.Success, result.Error);
            Assert.True(string.IsNullOrEmpty(result.SessionId));

            var updatedTank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, updatedTank!.CurrentVolume); // no changes

            var session = await db.FuellingSessions.FirstOrDefaultAsync();
            Assert.Null(session);
            Assert.Contains(string.Format(ErrorMessages.PumpNotAutoSelected, fuelType), result.Error);
        }
    }
    
    [Fact]
    public async Task StartFuelling_NoFreePumpManualSelect_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, pumpId, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        // set pump locked
        var redisLockMock = serviceProvider.GetRequiredService<IRedisLockProvider>();
        redisLockMock.IsLockedAsync(pumpId).Returns(true);
        redisLockMock.TryAcquireLockAsync(LockConstants.PumpLockKey(pumpId), Arg.Any<TimeSpan>()).Returns((RedisLockToken?)null);
        redisLockMock.TryAcquireLockWithRetryAsync(LockConstants.PumpLockKey(pumpId),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns((RedisLockToken?)null);
        
        //# Act
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: pumpId,
            fuelType: fuelType,
            preauthorizedLitres: volume,
            idempotencyKey: Guid.NewGuid().ToString()
        );
        
        //# Assert
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            Assert.False(result.Success, result.Error);
            Assert.True(string.IsNullOrEmpty(result.SessionId));

            var updatedTank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, updatedTank!.CurrentVolume); // no changes

            var session = await db.FuellingSessions.FirstOrDefaultAsync();
            Assert.Null(session);
            Assert.Contains(string.Format(ErrorMessages.PumpIsBusy, pumpId), result.Error);
        }
    }
    
    [Fact]
    public async Task StartFuelling_StationClosedFuellingRejected_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        // lock station
        var redisLockMock = serviceProvider.GetRequiredService<IRedisLockProvider>();
        redisLockMock.TryAcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns((RedisLockToken?)null);
        redisLockMock.TryAcquireLockWithRetryAsync(Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns((RedisLockToken?)null);
        redisLockMock.IsLockedAsync(Arg.Any<string>()).Returns(true);
        
        //# Act
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: null,                 // auto-select
            fuelType: fuelType,
            preauthorizedLitres: volume,
            idempotencyKey: Guid.NewGuid().ToString()
        );
        
        //# Assert
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            Assert.False(result.Success, result.Error);
            Assert.True(string.IsNullOrEmpty(result.SessionId));

            var updatedTank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, updatedTank!.CurrentVolume); // no changes

            var session = await db.FuellingSessions.FirstOrDefaultAsync();
            Assert.Null(session);
            Assert.Contains(string.Format(ErrorMessages.StationClosedFuellingRejected, stationId, fuelType), result.Error);
        }
    }

    [Fact]
    public async Task StartFuelling_FuelTypeMismatch_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, pumpId, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        //# Act
        const FuelType wrongFuelType = FuelType.Dt;
        const int volume = 50;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: pumpId,
            fuelType: wrongFuelType,
            preauthorizedLitres: volume,
            idempotencyKey: Guid.NewGuid().ToString()
        );
        
        //# Assert
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            Assert.False(result.Success, result.Error);
            Assert.True(string.IsNullOrEmpty(result.SessionId));

            var updatedTank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, updatedTank!.CurrentVolume); // no changes

            var session = await db.FuellingSessions.FirstOrDefaultAsync();
            Assert.Null(session);
            Assert.Contains(string.Format(ErrorMessages.FuelTypeMismatch), result.Error);
        }
    }
    
    [Fact]
    public async Task StartFuelling_TankNotFound_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, _, pumpId, nozzleId) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        var missingTankId = "missing-tank";
        using (var arrangeScope = scopeFactory.CreateScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var brokenNozzle = await db.Nozzles.FindAsync(nozzleId);
            brokenNozzle!.TankId = missingTankId;
            await db.SaveChangesAsync();
        }
        
        //# Act
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: pumpId,
            fuelType: fuelType,
            preauthorizedLitres: volume,
            idempotencyKey: Guid.NewGuid().ToString()
        );
        
        //# Assert
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            Assert.False(result.Success, result.Error);
            Assert.True(string.IsNullOrEmpty(result.SessionId));

            var session = await db.FuellingSessions.FirstOrDefaultAsync();
            Assert.Null(session);
            Assert.Contains(string.Format(ErrorMessages.TankNotFound, missingTankId), result.Error);
        }
    }
    
    [Fact]
    public async Task StartFuelling_NoIdempotencyKey_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        //# Act
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: null,                 // auto-select
            fuelType: fuelType,
            preauthorizedLitres: volume,
            idempotencyKey: ""
        );
        
        //# Assert
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            Assert.False(result.Success, result.Error);
            Assert.True(string.IsNullOrEmpty(result.SessionId));

            var updatedTank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, updatedTank!.CurrentVolume); // no changes

            var session = await db.FuellingSessions.FirstOrDefaultAsync();
            Assert.Null(session);
            Assert.Contains(string.Format(ErrorMessages.IdempotencyKeyNotProvidedForFuelling), result.Error);
        }
    }
    
    [Fact]
    public async Task StartFuelling_RepeatedRequestWithSameIdempotencyKey_ReturnsCachedResult()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        const int volume = 50;
        const string sessionId = "existing-session";
        const string idempotencyKey = "repeated-key";
        var idempotencyMock = serviceProvider.GetRequiredService<IRedisIdempotencyProvider>();
        var cachedResult = StartFuellingResult.Ok(sessionId, volume);
        idempotencyMock.GetIdempotencyResultAsync<StartFuellingResult>(idempotencyKey)
            .Returns(cachedResult);

        //# Act
        const FuelType fuelType = FuelType.Ai95;
        var result = await manager.StartFuellingAsync(
            stationId: stationId,
            pumpId: null,
            fuelType: fuelType,
            preauthorizedLitres: volume,
            idempotencyKey: idempotencyKey
        );

        //# Assert
        Assert.True(result.Success);
        Assert.Equal(sessionId, result.SessionId);

        using var assertScope = scopeFactory.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tank = await db.Tanks.FindAsync(tankId);
        Assert.Equal(100, tank!.CurrentVolume);
        // no new sessions created
        Assert.False(await db.FuellingSessions.AnyAsync());
    }

    [Fact]
    public async Task StartFuelling_ConcurrentRequestsWithSameKey_OnlyOneProcessed()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        var redisIdempotencyMock = serviceProvider.GetRequiredService<IRedisIdempotencyProvider>();

        // synchronization variables
        var firstCallCompleted = new TaskCompletionSource();
        var keyAcquired = new TaskCompletionSource();
        int keyAcquiredCount = 0;

        // setup TrySetIdempotencyKeyAsync: true on first invoke, false on any other
        redisIdempotencyMock.TrySetIdempotencyKeyAsync(Arg.Any<string>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref keyAcquiredCount) == 1)
                {
                    keyAcquired.TrySetResult();
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            });
        
        // capture result of first thread
        StartFuellingResult? savedResult = null;
        redisIdempotencyMock.SetIdempotencyResultAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(async callInfo =>
            {
                // fake save to use it later
                var json = callInfo.ArgAt<string>(1); // we need 2nd string argument
                savedResult = JsonSerializer.Deserialize<StartFuellingResult>(json);
                firstCallCompleted.TrySetResult();
                await Task.CompletedTask;
            });
        
        // WaitForIdempotentResultAsync will wait for first thread to save the result
        redisIdempotencyMock.WaitForIdempotentResultAsync<StartFuellingResult>(Arg.Any<string>())
            .Returns(async _ =>
            {
                await firstCallCompleted.Task;
                return savedResult;
            });

        // GetIdempotencyResultAsync returns null before saving data and - savedResult after saving
        redisIdempotencyMock.GetIdempotencyResultAsync<StartFuellingResult>(Arg.Any<string>())
            .Returns(_ =>
            {
                return Task.FromResult(savedResult);
            });
        
        //# Act
        // launch 2 parallel tasks
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        var task1 = manager.StartFuellingAsync(stationId, null, fuelType, volume, "same-key");
        var task2 = manager.StartFuellingAsync(stationId, null, fuelType, volume, "same-key");

        await Task.WhenAll(task1, task2);
        
        //# Assert
        var result1 = await task1;
        var result2 = await task2;
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Equal(result1.SessionId, result2.SessionId);

        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100 - volume, tank!.CurrentVolume); // single fuel transaction happened
            var sessions = await db.FuellingSessions.ToListAsync();
            Assert.Single(sessions); // only one session created
        }
    }
    #endregion
    
    #region CompleteFuelling

    [Fact]
    public async Task CompleteFuelling_ValidRequest_ReturnsSuccess()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, pumpId, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        const string sessionId = "session-1";
        await CreateFuellingSession(scopeFactory,
            sessionId, stationId, tankId, pumpId, fuelType, volume, SessionStatus.Reserved);
        using (var arrangeScope = scopeFactory.CreateScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            // simulate fuel reservation
            var tank = await db.Tanks.FindAsync(tankId);
            tank!.CurrentVolume -= volume;
            await db.SaveChangesAsync();
        }
        
        //# Act
        CompleteFuellingResult completeResult;
        const int actualVolume = volume - 10;
        using (var actScope = scopeFactory.CreateScope())
        {
            var db = actScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.FuellingSessions.FindAsync(sessionId);
            completeResult = await manager.CompleteFuellingAsync(stationId, session!.Id, actualVolume);
        }

        //# Assert
        Assert.True(completeResult.Success);
        
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.FuellingSessions.FirstOrDefaultAsync());
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100 - actualVolume, tank!.CurrentVolume);
        }
    }

    [Fact]
    public async Task CompleteFuelling_SessionNotFound_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);

        //# Act
        const int volume = 50;
        var sessionId = "missing-session";
        var completeResult = await manager.CompleteFuellingAsync(stationId, sessionId, volume);

        //# Assert
        Assert.False(completeResult.Success);
        
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.FuellingSessions.FirstOrDefaultAsync());
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, tank!.CurrentVolume);
            Assert.Contains(string.Format(ErrorMessages.FuellingSessionNotFound, sessionId), completeResult.Error);
        }
    }
    
    [Fact]
    public async Task CompleteFuelling_PumpNotFound_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, _, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        const string missingPumpId = "missing-pump";
        const string sessionId = "session-1";
        await CreateFuellingSession(scopeFactory,
            sessionId, stationId, tankId, missingPumpId, fuelType, volume, SessionStatus.Reserved);
        using (var arrangeScope = scopeFactory.CreateScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            // simulate fuel reservation
            var tank = await db.Tanks.FindAsync(tankId);
            tank!.CurrentVolume -= volume;
            await db.SaveChangesAsync();
        }

        //# Act
        var completeResult = await manager.CompleteFuellingAsync(stationId, sessionId, volume);

        //# Assert
        Assert.False(completeResult.Success);
        
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.FuellingSessions.FirstOrDefaultAsync());
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100 - volume, tank!.CurrentVolume);
            Assert.Contains(string.Format(ErrorMessages.PumpNotFound, missingPumpId), completeResult.Error);
        }
    }
    
    [Fact]
    public async Task CompleteFuelling_TankNotFound_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, pumpId, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        const string missingTankId = "missing-tank";
        const string sessionId = "session-1";
        await CreateFuellingSession(scopeFactory,
            sessionId, stationId, missingTankId, pumpId, fuelType, volume, SessionStatus.Reserved);

        //# Act
        var completeResult = await manager.CompleteFuellingAsync(stationId, sessionId, volume);

        //# Assert
        Assert.False(completeResult.Success);
        
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.FuellingSessions.FirstOrDefaultAsync());
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, tank!.CurrentVolume);
            Assert.Contains(string.Format(ErrorMessages.TankNotFound, missingTankId), completeResult.Error);
        }
    }

    [Fact]
    public async Task CompleteFuelling_SessionAlreadyCompleted_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, pumpId, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        const string sessionId = "session-1";
        await CreateFuellingSession(scopeFactory,
            sessionId, stationId, tankId, pumpId, fuelType, volume, SessionStatus.Completed);

        //# Act
        var completeResult = await manager.CompleteFuellingAsync(stationId, sessionId, volume);

        //# Assert
        Assert.False(completeResult.Success);
        
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.FuellingSessions.FirstOrDefaultAsync());
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100, tank!.CurrentVolume);
            Assert.Contains(string.Format(ErrorMessages.SessionAlreadyCompleted, sessionId), completeResult.Error);
        }
    }
    
    [Fact]
    public async Task CompleteFuelling_TankIsBusy_ReturnsError()
    {
        //# Arrange
        // base
        var (manager, serviceProvider, scopeFactory) = CreateManagerWithInMemoryDb();
        var (stationId, tankId, pumpId, _) = await TestHelpers.SeedDefaultDataToDbAsync(serviceProvider);
        
        var redisLockMock = serviceProvider.GetRequiredService<IRedisLockProvider>();
        redisLockMock.IsLockedAsync(LockConstants.TankLockKey(tankId)).Returns(true);
        redisLockMock.TryAcquireLockAsync(LockConstants.TankLockKey(tankId), Arg.Any<TimeSpan>()).Returns((RedisLockToken?)null);
        redisLockMock.TryAcquireLockWithRetryAsync(LockConstants.TankLockKey(tankId),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns((RedisLockToken?)null);
        
        const FuelType fuelType = FuelType.Ai95;
        const int volume = 50;
        const string sessionId = "session-1";
        await CreateFuellingSession(scopeFactory,
            sessionId, stationId, tankId, pumpId, fuelType, volume, SessionStatus.Reserved);
        using (var arrangeScope = scopeFactory.CreateScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            // simulate fuel reservation
            var tank = await db.Tanks.FindAsync(tankId);
            tank!.CurrentVolume -= volume;
            await db.SaveChangesAsync();
        }

        //# Act
        var completeResult = await manager.CompleteFuellingAsync(stationId, sessionId, volume);

        //# Assert
        Assert.False(completeResult.Success);
        
        using (var assertScope = scopeFactory.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null(await db.FuellingSessions.FirstOrDefaultAsync());
            var tank = await db.Tanks.FindAsync(tankId);
            Assert.Equal(100 - volume, tank!.CurrentVolume);
            Assert.Contains(string.Format(ErrorMessages.TankIsBusy, tankId), completeResult.Error);
        }
    }

    #endregion

    private (ReservationManager manager, ServiceProvider serviceProvider, IServiceScopeFactory scopeFactory) CreateManagerWithInMemoryDb()
    {
        var (serviceProvider, scopeFactory, redisLock, idempotency, _) = TestHelpers.CreateServiceProviderWithMocks();
        var manager = new ReservationManager(scopeFactory, redisLock, idempotency,
            serviceProvider.GetRequiredService<IOptions<SimulationConfig>>());
        return (manager, serviceProvider, scopeFactory);
    }

    private async Task CreateFuellingSession(IServiceScopeFactory scopeFactory,
        string sessionId, string stationId, string tankId, string pumpId, FuelType fuelType, int volumeToReserve, string status)
    {
        using var arrangeScope = scopeFactory.CreateScope();
        var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = new FuellingSessionEntity
        {
            Id = sessionId,
            StationId = stationId,
            TankId = tankId,
            PumpId = pumpId,
            FuelType = fuelType,
            ReservedVolume = volumeToReserve,
            Status = status
        };
        await db.FuellingSessions.AddAsync(session);
        await db.SaveChangesAsync();
    }
}