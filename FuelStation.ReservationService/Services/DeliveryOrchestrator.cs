using System.Collections.Concurrent;
using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FuelStation.ReservationService.Services;

public class DeliveryOrchestrator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisLockProvider _lockProvider;
    private readonly RedisIdempotencyProvider _idempotencyProvider;
    private readonly KafkaProducerService _kafka;
    private readonly SimulationConfig _simulationConfig;
    private readonly ILogger<DeliveryOrchestrator> _logger;

    private readonly ConcurrentDictionary<string, Task> _activeDeliveries = new();

    public DeliveryOrchestrator(IServiceScopeFactory scopeFactory,
        RedisLockProvider lockProvider,
        RedisIdempotencyProvider idempotencyProvider,
        KafkaProducerService kafka, IOptions<SimulationConfig> deliveryOptions, ILogger<DeliveryOrchestrator> logger)
    {
        _scopeFactory = scopeFactory;
        _lockProvider = lockProvider;
        _idempotencyProvider = idempotencyProvider;
        _kafka = kafka;
        _simulationConfig = deliveryOptions.Value;
        _logger = logger;
    }

    public async Task<StartDeliveryResult> StartDeliveryProcessAsync(string stationId, List<Compartment> compartments, string idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
            return StartDeliveryResult.Fail(ErrorMessages.IdempotencyKeyNotProvidedForDelivering);
        
        var cachedOperationResult = await _idempotencyProvider.GetIdempotencyResultAsync<StartDeliveryResult>(idempotencyKey);
        if (cachedOperationResult != null)
            return cachedOperationResult;

        var keyAcquired = await _idempotencyProvider.TrySetIdempotencyKeyAsync(idempotencyKey);
        if (keyAcquired == false)
        {
            cachedOperationResult = await _idempotencyProvider.WaitForIdempotentResultAsync<StartDeliveryResult>(idempotencyKey);
            return cachedOperationResult ?? StartDeliveryResult.Fail(ErrorMessages.IdempotencyConflict);
        }
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var session = new DeliverySessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            StationId = stationId,
            Compartments = compartments.Select(c => new DeliveryCompartmentEntity
            {
                Id = Guid.NewGuid().ToString(),
                FuelType = c.FuelType,
                Litres = c.Litres
            }).ToList(),
            Status = DeliverySessionStatus.Scheduled
        }; 
        db.DeliverySessions.Add(session);
        await db.SaveChangesAsync();
        var deliveryTask = Task.Run(() => ExecuteDeliveryProcess(session.Id, stationId, compartments));
        _activeDeliveries.TryAdd(session.Id, deliveryTask);
        _logger.LogInformation("Delivery {SessionId} for station {StationId} started in background", session.Id, stationId);
        var okResult = StartDeliveryResult.Ok(session.Id);
        await _idempotencyProvider.SetIdempotencyResultAsync(idempotencyKey, okResult.ToString());
        return okResult;
    }

    private async Task ExecuteDeliveryProcess(string sessionId, string stationId, List<Compartment> compartments)
    {
        RedisLockToken? stationLock = null;
        bool lockAcquired = false;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        try
        {
            _logger.LogInformation(">>> ExecuteDeliveryProcess STARTED for session {0}", sessionId);
            var sessionEntity = await db.DeliverySessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (sessionEntity == null)
                throw new InvalidOperationException(ErrorMessages.DeliverySessionNotFound);
            
            sessionEntity.Status = DeliverySessionStatus.Scheduled;
            await db.SaveChangesAsync();
            await _kafka.SendDeliveryEvent(stationId, sessionId, DeliverySessionStatus.Scheduled.ToString());
            
            // delivery
            await Task.Delay(GetDeliveryTime());

            // arrived
            stationLock = await _lockProvider.TryAcquireLockAsync(
                LockConstants.StationLockKey(stationId), TimeSpan.FromSeconds(LockConstants.StationLockExpireTime));
            if (stationLock == null)
                throw new InvalidOperationException( ErrorMessages.StationClosedForDelivering);

            lockAcquired = true;
            sessionEntity.Status = DeliverySessionStatus.Arrived;
            await db.SaveChangesAsync();
            await _kafka.SendDeliveryEvent(stationId, sessionId, DeliverySessionStatus.Arrived.ToString());

            // unloading
            await Task.Delay(GetUnloadTime());
            await AddFuel(db, stationId, compartments);

            // completed
            sessionEntity.Status = DeliverySessionStatus.Completed;
            await db.SaveChangesAsync();
            await _kafka.SendDeliveryEvent(stationId, sessionId, DeliverySessionStatus.Completed.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delivery {SessionId} failed unexpectedly", sessionId);
            
            // try save session as Failed
            try
            {
                var sessionEntity = await db.DeliverySessions.FirstOrDefaultAsync(s => s.Id == sessionId);
                if (sessionEntity != null)
                {
                    sessionEntity.Status = DeliverySessionStatus.Failed;
                    await db.SaveChangesAsync();
                }

                await _kafka.SendDeliveryEvent(stationId, sessionId, DeliverySessionStatus.Failed.ToString());
            }
            catch (Exception exInner)
            {
                _logger.LogError(exInner, "Saving Delivery {SessionId} as Failed was unsuccessful", sessionId);
            }
        }
        finally
        {
            if (lockAcquired && stationLock != null)
                await _lockProvider.ReleaseLockAsync(stationLock);
            _activeDeliveries.TryRemove(sessionId, out _);
        }
    }

    private async Task AddFuel(AppDbContext db, string stationId, List<Compartment> compartments)
    {
        foreach (var compartment in compartments)
        {
            var tanks = db.Tanks.Where(t => t.StationId == stationId && t.FuelType == compartment.FuelType).ToList();
            if (tanks.Count == 0)
                continue;
                
            var fuelToAddForSingleTank = (decimal)compartment.Litres / tanks.Count;

            foreach (var tank in tanks)
            {
                RedisLockToken? tankLock = null;
                bool acquired = false;

                for (int retry = 0; retry < _simulationConfig.MaxTankFillRetriesCount; retry++)
                {
                    tankLock = await _lockProvider.TryAcquireLockAsync(
                        LockConstants.TankLockKey(tank.Id), TimeSpan.FromSeconds(LockConstants.TankLockExpireTime));
                    if (tankLock != null)
                    {
                        acquired = true;
                        break;
                    }
                    if (retry < _simulationConfig.MaxTankFillRetriesCount - 1)
                        await Task.Delay(_simulationConfig.TankFillRetryDelayMs);
                }

                if (!acquired)
                {
                    throw new InvalidOperationException( string.Format(ErrorMessages.TankIsBusy, tank.Id));
                }

                try
                {
                    // double-check
                    await db.Entry(tank).ReloadAsync();
                    var freeSpace = tank.Capacity - tank.CurrentVolume;
                    var fuelToAdd = Math.Min(fuelToAddForSingleTank, freeSpace);
                    tank.CurrentVolume += fuelToAdd;
                    await _lockProvider.SetTankVolumeAsync(tank.Id, tank.CurrentVolume);
                }
                finally
                {
                    if (acquired && tankLock != null)
                        await _lockProvider.ReleaseLockAsync(tankLock);
                }
            }
        }
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }

        // wait for active deliveries to complete
        if (!_activeDeliveries.IsEmpty)
        {
            var activeTasks = _activeDeliveries.Values.ToArray();
            await Task.WhenAll(activeTasks);
        }
    }
    
    private int GetDeliveryTime()
    {
        int minutes = Random.Shared.Next(_simulationConfig.MinDeliveryDurationMinutes, _simulationConfig.MaxDeliveryDurationMinutes);
        return minutes * 60 * 1000 / _simulationConfig.SpeedFactor;
    }

    private int GetUnloadTime()
    {
        int minutes = Random.Shared.Next(_simulationConfig.MinUnloadDurationMinutes, _simulationConfig.MaxUnloadDurationMinutes);
        return minutes * 60 * 1000 / _simulationConfig.SpeedFactor;
    }
}