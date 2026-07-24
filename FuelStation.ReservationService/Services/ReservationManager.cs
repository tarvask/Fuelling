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
    private readonly RedisLockProvider _lockProvider;
    private readonly ConcurrentDictionary<string, RedisLockToken> _pumpLocks = new();
    private RedisLockToken? _currentStationLock;

    public ReservationManager(IServiceScopeFactory scopeFactory, RedisLockProvider lockProvider)
    {
        _scopeFactory = scopeFactory;
        _lockProvider = lockProvider;
    }

    public async Task<StartFuellingResult> StartFuellingAsync(string stationId, string? pumpId, FuelType fuelType, double preauthorizedLitres)
    {
        if (_currentStationLock != null)
            return StartFuellingResult.Fail(string.Format(ErrorMessages.StationClosed, fuelType));

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var (pump, pumpLock) = await SelectAndLockPumpAsync(db, stationId, pumpId, fuelType);
        if (pump == null || pumpLock == null)
            return StartFuellingResult.Fail(pumpLock == null
                ? string.Format(ErrorMessages.PumpNotAutoSelected, fuelType)
                : ErrorMessages.PumpIsBusy);
        
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
                    RedisConstants.TankLockKey(tank.Id), TimeSpan.FromSeconds(RedisConstants.TankLockExpireTime));
                if (tankLock == null)
                    return StartFuellingResult.Fail(ErrorMessages.TankIsBusy);

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
                return StartFuellingResult.Ok(session.Id, (double)reserve);
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
            await ReleaseAndCleanupAsync(pumpLock, session, db, sessionId);
            return CompleteFuellingResult.Fail(ErrorMessages.PumpNotFound);
        }
        
        var tank = await db.Tanks.FirstOrDefaultAsync(t => t.Id == session.TankId && t.StationId == stationId);
        if (tank == null)
        {
            await ReleaseAndCleanupAsync(pumpLock, session, db, sessionId);
            return CompleteFuellingResult.Fail(ErrorMessages.TankNotFound);
        }

        if (session.Status != SessionStatus.Reserved)
        {
            await ReleaseAndCleanupAsync(pumpLock, session, db, sessionId);
            return CompleteFuellingResult.Fail(ErrorMessages.SessionAlreadyCompleted);
        }
        
        RedisLockToken? tankLock = null;
        try
        {
            tankLock = await _lockProvider.TryAcquireLockAsync(
                RedisConstants.TankLockKey(tank.Id), TimeSpan.FromSeconds(RedisConstants.TankLockExpireTime));
            if (tankLock == null)
            {
                await ReleaseAndCleanupAsync(pumpLock, session, db, sessionId);
                return CompleteFuellingResult.Fail(ErrorMessages.TankIsBusy);
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
            await ReleaseAndCleanupAsync(pumpLock, session, db, sessionId);
        }
    }

    public async Task<AddFuelResult> AddFuelFastAsync(string stationId, FuelType fuelType, double litres)
    {
        if (litres <= 0) return AddFuelResult.Fail(ErrorMessages.LitresMustBePositive);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var tank = await db.Tanks
            .Where(t => t.StationId == stationId && t.FuelType == fuelType)
            .OrderBy(t => t.CurrentVolume)
            .FirstOrDefaultAsync();
        if (tank == null)
            return AddFuelResult.Fail(ErrorMessages.NoTankForFuelType);
        
        RedisLockToken? tankLock = null;
        try
        {
            tankLock = await _lockProvider.TryAcquireLockAsync(
                RedisConstants.TankLockKey(tank.Id), TimeSpan.FromSeconds(RedisConstants.TankLockExpireTime));
            if (tankLock == null)
            {
                return AddFuelResult.Fail(ErrorMessages.TankIsBusy);
            }

            // double check
            await db.Entry(tank).ReloadAsync();
            var freeSpace = tank.Capacity - tank.CurrentVolume;
            var fuelToAdd = Math.Min((decimal)litres, freeSpace);
            tank.CurrentVolume += fuelToAdd;
            await db.SaveChangesAsync();
            await _lockProvider.SetTankVolumeAsync(tank.Id, tank.CurrentVolume);
            return AddFuelResult.Ok(tank.Id, (double)tank.CurrentVolume);
        }
        finally
        {
            if (tankLock != null) await _lockProvider.ReleaseLockAsync(tankLock);
        }
    }

    public async Task<StartDeliveryResult> StartDeliveryAsync(string stationId, List<Compartment> compartments)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        RedisLockToken? stationLock = await _lockProvider.TryAcquireLockAsync(
            RedisConstants.StationDeliveryLockKey, TimeSpan.FromSeconds(RedisConstants.StationLockExpireTime));
        if (stationLock == null)
        {
            return StartDeliveryResult.Fail(ErrorMessages.DeliveryInProgress);
        }

        try
        {
            var session = new DeliverySessionEntity
            {
                Id = Guid.NewGuid().ToString(),
                StationId = stationId,
                Compartments = compartments.Select(c => new DeliveryCompartmentEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    FuelType = c.FuelType,
                    Litres = c.Litres
                }).ToList()
            };
            db.DeliverySessions.Add(session);
            await db.SaveChangesAsync();
            _currentStationLock = stationLock;
            return StartDeliveryResult.Ok(session.Id);
        }
        catch
        {
            await _lockProvider.ReleaseLockAsync(stationLock);
            throw;
        }
    }

    public async Task<CompleteDeliveryResult> CompleteDeliveryAsync(string stationId, string sessionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = await db.DeliverySessions
            .Include(s => s.Compartments)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.StationId == stationId);
        if (session == null)
            return CompleteDeliveryResult.Fail(ErrorMessages.DeliverySessionNotFound);

        if (_currentStationLock == null)
        {
            db.DeliverySessions.Remove(session);
            await db.SaveChangesAsync();
            return CompleteDeliveryResult.Fail(ErrorMessages.DeliverySessionNotFound);
        }
        
        try
        {
            foreach (var compartment in session.Compartments)
            {
                var tanks = db.Tanks.Where(t => t.StationId == stationId && t.FuelType == compartment.FuelType).ToList();
                if (tanks.Count == 0)
                    continue;
                
                var fuelToAddForSingleTank = (decimal)compartment.Litres / tanks.Count;

                foreach (var tank in tanks)
                {
                    RedisLockToken? tankLock = null;

                    try
                    {
                        tankLock = await _lockProvider.TryAcquireLockAsync(
                            RedisConstants.TankLockKey(tank.Id), TimeSpan.FromSeconds(RedisConstants.TankLockExpireTime));
                        if (tankLock == null)
                        {
                            return CompleteDeliveryResult.Fail(ErrorMessages.TankIsBusy);
                        }
                        
                        // double-check
                        await db.Entry(tank).ReloadAsync();
                        var freeSpace = tank.Capacity - tank.CurrentVolume;
                        var fuelToAdd = Math.Min(fuelToAddForSingleTank, freeSpace);
                        tank.CurrentVolume += fuelToAdd;
                        await _lockProvider.SetTankVolumeAsync(tank.Id, tank.CurrentVolume);
                    }
                    finally
                    {
                        if (tankLock != null)
                            await _lockProvider.ReleaseLockAsync(tankLock);
                    }
                }
            }
            
            db.DeliverySessions.Remove(session);
            await db.SaveChangesAsync();
            return CompleteDeliveryResult.Ok();
        }
        finally
        {
            if (_currentStationLock != null)
            {
                await _lockProvider.ReleaseLockAsync(_currentStationLock);
                _currentStationLock = null;
            }
        }
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
                    RedisConstants.PumpLockKey(candidate.Id), TimeSpan.FromSeconds(RedisConstants.PumpLockExpireTime));
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
            RedisConstants.PumpLockKey(pump.Id), TimeSpan.FromSeconds(RedisConstants.PumpLockExpireTime));
        return (pump, pumpLock);
    }
    
    private async Task ReleaseAndCleanupAsync(RedisLockToken? pumpLock, FuellingSessionEntity session, AppDbContext db, string sessionId)
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