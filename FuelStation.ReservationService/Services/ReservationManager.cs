using System.Collections.Concurrent;
using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FuelStation.ReservationService.Services;

public class ReservationManager
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRedisLockProvider _lockProvider;
    private readonly IRedisIdempotencyProvider _idempotencyProvider;
    
    private readonly ConcurrentDictionary<string, RedisLockToken> _pumpLocks = new();

    public ReservationManager(IServiceScopeFactory scopeFactory, IRedisLockProvider lockProvider, IRedisIdempotencyProvider idempotencyProvider)
    {
        _scopeFactory = scopeFactory;
        _lockProvider = lockProvider;
        _idempotencyProvider = idempotencyProvider;
    }

    public async Task<StartFuellingResult> StartFuellingAsync(string stationId, string? pumpId, FuelType fuelType, double preauthorizedLitres, string idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
            return StartFuellingResult.Fail(ErrorMessages.IdempotencyKeyNotProvidedForFuelling);
        
        var cachedOperationResult = await _idempotencyProvider.GetIdempotencyResultAsync<StartFuellingResult>(idempotencyKey);
        if (cachedOperationResult != null)
            return cachedOperationResult;
        
        var keyAcquired = await _idempotencyProvider.TrySetIdempotencyKeyAsync(idempotencyKey);
        if (keyAcquired == false)
        {
            var eventualResult = await _idempotencyProvider.WaitForIdempotentResultAsync<StartFuellingResult>(idempotencyKey);
            return eventualResult ?? StartFuellingResult.Fail(ErrorMessages.IdempotencyConflict);
        }
        
        if (await _lockProvider.IsLockedAsync(LockConstants.StationLockKey(stationId)))
            return StartFuellingResult.Fail(string.Format(ErrorMessages.StationClosedForFuelling, fuelType));
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var (pump, pumpLock) = await SelectAndLockPumpAsync(db, stationId, pumpId, fuelType);
        if (pump == null || pumpLock == null)
            return StartFuellingResult.Fail(pumpLock == null
                ? string.Format(ErrorMessages.PumpNotAutoSelected, fuelType)
                : string.Format(ErrorMessages.PumpIsBusy, pumpId));
        
        try
        {
            var nozzle = pump.Nozzles.FirstOrDefault(n => n.FuelType == fuelType);
            if (nozzle == null)
                return StartFuellingResult.Fail(ErrorMessages.FuelTypeMismatch);

            var tank = nozzle.Tank;
            if (tank == null)
                return StartFuellingResult.Fail(ErrorMessages.TankNotFound);

            if (tank.CurrentVolume <= 0)
                return StartFuellingResult.Fail(string.Format(ErrorMessages.NoFuelAvailable, tank.Id));
            
            RedisLockToken? tankLock = null;
            try
            {
                tankLock = await _lockProvider.TryAcquireLockAsync(
                    LockConstants.TankLockKey(tank.Id), TimeSpan.FromSeconds(LockConstants.TankLockExpireTime));
                if (tankLock == null)
                    return StartFuellingResult.Fail(string.Format(ErrorMessages.TankIsBusy, tank.Id));

                // double check
                if (tank.CurrentVolume <= 0)
                    return StartFuellingResult.Fail(string.Format(ErrorMessages.NoFuelAvailable, tank.Id));

                decimal reserve = Math.Min((decimal)preauthorizedLitres, tank.CurrentVolume);
                tank.CurrentVolume -= reserve;

                var session = new FuellingSessionEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    StationId = stationId,
                    PumpId = pump.Id,
                    TankId = tank.Id,
                    FuelType = fuelType,
                    ReservedVolume = reserve,
                    Status = SessionStatus.Reserved
                };
                db.FuellingSessions.Add(session);

                await db.SaveChangesAsync();
                await _lockProvider.SetTankVolumeAsync(tank.Id, tank.CurrentVolume);

                _pumpLocks[session.Id] = pumpLock;
                
                var okResult = StartFuellingResult.Ok(session.Id, (double)reserve);
                await _idempotencyProvider.SetIdempotencyResultAsync(idempotencyKey, okResult.ToString());
                return okResult;
            }
            finally
            {
                if (tankLock != null) await _lockProvider.ReleaseLockAsync(tankLock);
            }
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
            return CompleteFuellingResult.Fail(ErrorMessages.FuellingSessionNotFound);
        
        RedisLockToken? pumpLock;
        _pumpLocks.TryGetValue(sessionId, out pumpLock);

        var pump = await db.Pumps.FirstOrDefaultAsync(p => p.Id == session.PumpId && p.StationId == stationId);
        if (pump == null)
        {
            await ReleasePumpAndCleanupAsync(pumpLock, session, db, sessionId);
            return CompleteFuellingResult.Fail(ErrorMessages.PumpNotFound);
        }
        
        var tank = await db.Tanks.FirstOrDefaultAsync(t => t.Id == session.TankId && t.StationId == stationId);
        if (tank == null)
        {
            await ReleasePumpAndCleanupAsync(pumpLock, session, db, sessionId);
            return CompleteFuellingResult.Fail(ErrorMessages.TankNotFound);
        }

        if (session.Status != SessionStatus.Reserved)
        {
            await ReleasePumpAndCleanupAsync(pumpLock, session, db, sessionId);
            return CompleteFuellingResult.Fail(ErrorMessages.SessionAlreadyCompleted);
        }
        
        RedisLockToken? tankLock = null;
        try
        {
            tankLock = await _lockProvider.TryAcquireLockAsync(
                LockConstants.TankLockKey(tank.Id), TimeSpan.FromSeconds(LockConstants.TankLockExpireTime));
            if (tankLock == null)
            {
                await ReleasePumpAndCleanupAsync(pumpLock, session, db, sessionId);
                return CompleteFuellingResult.Fail(string.Format(ErrorMessages.TankIsBusy, tank.Id));
            }

            decimal actual = Math.Min((decimal)actualLitres, session.ReservedVolume);
            decimal leftover = session.ReservedVolume - actual;
            tank.CurrentVolume += leftover;

            session.ActualVolume = actual;
            session.Status = SessionStatus.Completed;

            db.FuellingSessions.Remove(session);
            await db.SaveChangesAsync();
            await _lockProvider.SetTankVolumeAsync(tank.Id, tank.CurrentVolume);
            
            return CompleteFuellingResult.Ok();
        }
        finally
        {
            if (tankLock != null) await _lockProvider.ReleaseLockAsync(tankLock);
            await ReleasePumpAndCleanupAsync(pumpLock, session, db, sessionId);
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
            .Include(p => p.Nozzles).ThenInclude(n => n.Tank)
            .FirstOrDefaultAsync(p => p.StationId == stationId && p.Id == pumpId);
        if (pump == null)
            return (null, null);

        pumpLock = await _lockProvider.TryAcquireLockAsync(
            LockConstants.PumpLockKey(pump.Id), TimeSpan.FromSeconds(LockConstants.PumpLockExpireTime));
        return (pump, pumpLock);
    }
    
    private async Task ReleasePumpAndCleanupAsync(RedisLockToken? pumpLock, FuellingSessionEntity session, AppDbContext db, string sessionId)
    {
        if (pumpLock != null)
            await _lockProvider.ReleaseLockAsync(pumpLock);
        _pumpLocks.TryRemove(sessionId, out _);
        
        if (db.Entry(session).State != EntityState.Detached)
        {
            db.FuellingSessions.Remove(session);
            await db.SaveChangesAsync();
        }
    }
}