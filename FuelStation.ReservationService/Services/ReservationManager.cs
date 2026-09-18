using System.Collections.Concurrent;
using System.Text.Json;
using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.ReservationService.Metrics;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FuelStation.ReservationService.Services;

public class ReservationManager
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRedisLockProvider _lockProvider;
    private readonly IRedisIdempotencyProvider _idempotencyProvider;
    private readonly SimulationConfig _simulationConfig;
    
    private readonly ConcurrentDictionary<string, RedisLockToken> _pumpLocks = new();

    public ReservationManager(IServiceScopeFactory scopeFactory, IRedisLockProvider lockProvider, IRedisIdempotencyProvider idempotencyProvider,
        IOptions<SimulationConfig> options)
    {
        _scopeFactory = scopeFactory;
        _lockProvider = lockProvider;
        _idempotencyProvider = idempotencyProvider;
        _simulationConfig = options.Value;
    }

    public async Task<StartFuellingResult> StartFuellingAsync(string stationId, string? pumpId, FuelType fuelType, double preauthorizedLitres, string idempotencyKey)
    {
        var idempotencyResult = await CheckOrAcquireIdempotencyAsync(stationId, idempotencyKey);
        if (idempotencyResult != null)
            return idempotencyResult;
        
        if (await _lockProvider.IsLockedAsync(LockConstants.StationLockKey(stationId)))
            return StartFuellingResult.Fail(stationId, ErrorCatalog.StationClosedForFuelling, fuelType);
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var (pump, pumpLock) = await SelectAndLockPumpAsync(db, stationId, pumpId, fuelType);
        if (pump == null || pumpLock == null)
            return string.IsNullOrEmpty(pumpId)
                ? StartFuellingResult.Fail(stationId, ErrorCatalog.PumpNotAutoSelected, fuelType)
                : StartFuellingResult.Fail(stationId, ErrorCatalog.PumpIsBusy, pumpId);
        
        try
        {
            var reserveResult = await ReserveFuelAndCreateSessionAsync(db, pump, fuelType, preauthorizedLitres, stationId,
                idempotencyKey, pumpLock);

            if (reserveResult.Success == false)
                await _lockProvider.ReleaseLockAsync(pumpLock);
            
            return reserveResult;
        }
        catch
        {
            await _lockProvider.ReleaseLockAsync(pumpLock);
            throw;
        }
    }

    public async Task<CompleteFuellingResult> CompleteFuellingAsync(string stationId, string sessionId, double actualLitres)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = await db.FuellingSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.StationId == stationId);
        if (session == null)
            return CompleteFuellingResult.Fail(stationId, ErrorCatalog.FuellingSessionNotFound, sessionId);
        
        RedisLockToken? pumpLock;
        _pumpLocks.TryGetValue(sessionId, out pumpLock);

        var pump = await db.Pumps.FirstOrDefaultAsync(p => p.Id == session.PumpId && p.StationId == stationId);
        if (pump == null)
        {
            await MarkSessionFailedAsync(db, session);
            await ReleasePumpLockAsync(pumpLock, sessionId);
            return CompleteFuellingResult.Fail(stationId, ErrorCatalog.PumpNotFound, session.PumpId);
        }
        
        var tank = await db.Tanks.FirstOrDefaultAsync(t => t.Id == session.TankId && t.StationId == stationId);
        if (tank == null)
        {
            await MarkSessionFailedAsync(db, session);
            await ReleasePumpLockAsync(pumpLock, sessionId);
            return CompleteFuellingResult.Fail(stationId, ErrorCatalog.TankNotFound, session.TankId);
        }

        if (session.Status != SessionStatus.Reserved)
        {
            if (session.Status != SessionStatus.Completed)
                await MarkSessionFailedAsync(db, session);
            
            await ReleasePumpLockAsync(pumpLock, sessionId);
            return CompleteFuellingResult.Fail(stationId, ErrorCatalog.SessionAlreadyCompleted, sessionId);
        }
        
        RedisLockToken? tankLock = null;
        try
        {
            tankLock = await _lockProvider.TryAcquireLockWithRetryAsync(
                LockConstants.TankLockKey(tank.Id), LockConstants.TankLockExpireTime, _simulationConfig.MaxFuellingRetriesCount, _simulationConfig.FuellingRetryDelayMs);
            if (tankLock == null)
            {
                await MarkSessionFailedAsync(db, session);
                return CompleteFuellingResult.Fail(stationId, ErrorCatalog.TankIsBusy, tank.Id);
            }

            decimal actual = Math.Min((decimal)actualLitres, session.ReservedVolume);
            decimal leftover = session.ReservedVolume - actual;
            tank.CurrentVolume += leftover;

            session.ActualVolume = actual;
            session.Status = SessionStatus.Completed;
            session.FinishedAt = DateTime.UtcNow;
            
            await db.SaveChangesAsync();
            await _lockProvider.SetTankVolumeAsync(tank.Id, tank.CurrentVolume);
            FuelStationMetrics.TankVolume.WithLabels(stationId, tank.Id, $"{session.FuelType}").Set((double)tank.CurrentVolume);
            var duration = (session.FinishedAt.Value - session.StartedAt).TotalSeconds;
            FuelStationMetrics.FuellingDuration.Observe(duration);
            
            return CompleteFuellingResult.Ok();
        }
        finally
        {
            if (tankLock != null) await _lockProvider.ReleaseLockAsync(tankLock);
            await ReleasePumpLockAsync(pumpLock, sessionId);
        }
    }

    public async Task<List<StationInfo>> GetStationsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Stations
            .Select(s => new StationInfo
            {
                Id = s.Id,
                Name = s.Name,
                Address = s.Address
            })
            .ToListAsync();
    }
    
    private async Task<StartFuellingResult?> CheckOrAcquireIdempotencyAsync(string stationId, string idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
            return StartFuellingResult.Fail(stationId, ErrorCatalog.IdempotencyKeyNotProvidedForFuelling);
        
        var cachedOperationResult = await _idempotencyProvider.GetIdempotencyResultAsync<StartFuellingResult>(idempotencyKey);
        if (cachedOperationResult != null)
            return cachedOperationResult;
        
        var keyAcquired = await _idempotencyProvider.TrySetIdempotencyKeyAsync(idempotencyKey);
        if (keyAcquired == false)
        {
            var eventualResult = await _idempotencyProvider.WaitForIdempotentResultAsync<StartFuellingResult>(idempotencyKey);
            return eventualResult ?? StartFuellingResult.Fail(stationId, ErrorCatalog.IdempotencyConflict);
        }

        // the key is acquire, we can continue
        return null;
    }

    private async Task<(PumpEntity? pump, RedisLockToken? lockToken)> SelectAndLockPumpAsync(
        AppDbContext db, string stationId, string? pumpId, FuelType fuelType)
    {
        RedisLockToken? pumpLock;
        if (string.IsNullOrEmpty(pumpId))
        {
            var candidates = await db.Pumps
                .Include(p => p.Nozzles).ThenInclude(n => n.Tank)
                .Where(p => p.StationId == stationId && p.Nozzles.Any(n => n.FuelType == fuelType && n.Tank.CurrentVolume > 0))
                .ToListAsync();

            foreach (var candidate in candidates)
            {
                pumpLock = await _lockProvider.TryAcquireLockAsync(
                    LockConstants.PumpLockKey(candidate.Id), TimeSpan.FromSeconds(LockConstants.PumpLockExpireTime));
                if (pumpLock != null)
                    return (candidate, pumpLock);
            }
            return (null, null);
        }

        var pump = await db.Pumps
            .Include(p => p.Nozzles)
            .FirstOrDefaultAsync(p => p.StationId == stationId && p.Id == pumpId);
        if (pump == null)
            return (null, null);

        pumpLock = await _lockProvider.TryAcquireLockAsync(
            LockConstants.PumpLockKey(pump.Id), TimeSpan.FromSeconds(LockConstants.PumpLockExpireTime));
        return (pump, pumpLock);
    }
    
    private async Task<StartFuellingResult> ReserveFuelAndCreateSessionAsync(
        AppDbContext db,
        PumpEntity pump,
        FuelType fuelType,
        double preauthorizedLitres,
        string stationId,
        string idempotencyKey,
        RedisLockToken pumpLock)
    {
        var nozzle = pump.Nozzles.FirstOrDefault(n => n.FuelType == fuelType);
        if (nozzle == null)
            return StartFuellingResult.Fail(stationId, ErrorCatalog.FuelTypeMismatch);

        var tank = await db.Tanks.FindAsync(nozzle.TankId);
        if (tank == null)
            return StartFuellingResult.Fail(stationId, ErrorCatalog.TankNotFound, nozzle.TankId);

        if (tank.CurrentVolume <= 0)
            return StartFuellingResult.Fail(stationId, ErrorCatalog.NoFuelAvailable, tank.Id);
            
        RedisLockToken? tankLock = null;
        try
        {
            tankLock = await _lockProvider.TryAcquireLockWithRetryAsync(
                LockConstants.TankLockKey(tank.Id), LockConstants.TankLockExpireTime, _simulationConfig.MaxFuellingRetriesCount, _simulationConfig.FuellingRetryDelayMs);
            if (tankLock == null)
                return StartFuellingResult.Fail(stationId, ErrorCatalog.TankIsBusy, tank.Id);

            // double check
            await db.Entry(tank).ReloadAsync();
            if (tank.CurrentVolume <= 0)
                return StartFuellingResult.Fail(stationId, ErrorCatalog.NoFuelAvailable, tank.Id);

            decimal reserve = Math.Min((decimal)preauthorizedLitres, tank.CurrentVolume);
            tank.CurrentVolume -= reserve;

            var session = new FuellingSessionEntity
            {
                Id = $"{Guid.NewGuid()}",
                StationId = stationId,
                PumpId = pump.Id,
                TankId = tank.Id,
                FuelType = fuelType,
                ReservedVolume = reserve,
                Status = SessionStatus.Reserved,
                StartedAt = DateTime.UtcNow
            };
            db.FuellingSessions.Add(session);

            await db.SaveChangesAsync();
            await _lockProvider.SetTankVolumeAsync(tank.Id, tank.CurrentVolume);
            FuelStationMetrics.TankVolume.WithLabels(stationId, tank.Id, $"{session.FuelType}").Set((double)tank.CurrentVolume);

            _pumpLocks[session.Id] = pumpLock;
                
            var okResult = StartFuellingResult.Ok(session.Id, (double)reserve);
            await _idempotencyProvider.SetIdempotencyResultAsync(idempotencyKey, JsonSerializer.Serialize(okResult));
            return okResult;
        }
        finally
        {
            if (tankLock != null)
                await _lockProvider.ReleaseLockAsync(tankLock);
        }
    }
    
    private async Task ReleasePumpLockAsync(RedisLockToken? pumpLock, string sessionId)
    {
        if (pumpLock != null)
            await _lockProvider.ReleaseLockAsync(pumpLock);
        _pumpLocks.TryRemove(sessionId, out _);
    }

    private static async Task MarkSessionFailedAsync(AppDbContext db, FuellingSessionEntity session)
    {
        session.Status = SessionStatus.Failed;
        session.FinishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}