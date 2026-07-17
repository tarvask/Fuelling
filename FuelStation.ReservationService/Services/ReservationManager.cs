using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Models;
using FuelStation.ReservationService.Persistence;
using FuelStation.ReservationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FuelStation.ReservationService.Services;

public class ReservationManager
{
    private readonly IServiceScopeFactory _scopeFactory;
    private volatile bool _isDeliveryInProgress; 
    private readonly object _lock = new();

    public ReservationManager(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<StartFuellingResult> StartFuellingAsync(string pumpId, FuelType fuelType, double preauthorizedLitres)
    {
        if (_isDeliveryInProgress)
            return StartFuellingResult.Fail(string.Format(ErrorMessages.StationClosed, fuelType));

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        PumpEntity? pump;
        
        if (string.IsNullOrEmpty(pumpId))
        {
            pump = await db.Pumps
                .Include(p => p.Nozzles)
                .ThenInclude(n => n.Tank)
                .FirstOrDefaultAsync(p => p.IsBusy == false
                                          && p.Nozzles.Any(n => n.FuelType == fuelType && n.Tank.CurrentVolume > 0));
            
            if (pump == null)
                return StartFuellingResult.Fail(string.Format(ErrorMessages.PumpNotAutoSelected, fuelType));
        }
        else
        {
            pump = await db.Pumps
                .Include(p => p.Nozzles)
                .FirstOrDefaultAsync(p => p.Id == pumpId);
            if (pump == null)
                return StartFuellingResult.Fail(ErrorMessages.PumpNotFound);
            if (pump.IsBusy)
                return StartFuellingResult.Fail(ErrorMessages.PumpIsBusy);
        }

        var nozzle = pump.Nozzles.FirstOrDefault(n => n.FuelType == fuelType);
        if (nozzle == null)
            return StartFuellingResult.Fail(ErrorMessages.FuelTypeMismatch);

        var tank = await db.Tanks.FindAsync(nozzle.TankId);
        if (tank == null)
            return StartFuellingResult.Fail(ErrorMessages.TankNotFound);
        
        if (tank.CurrentVolume <= 0)
            return StartFuellingResult.Fail(string.Format(ErrorMessages.NoFuelAvailable, tank.Id));
        
        lock (_lock)
        {
            // double check
            if (pump.IsBusy)
                return StartFuellingResult.Fail(ErrorMessages.PumpIsBusy);
            
            // double check
            if (tank.CurrentVolume <= 0)
                return StartFuellingResult.Fail(string.Format(ErrorMessages.NoFuelAvailable, tank.Id));
            
            pump.IsBusy = true;
            decimal reserve = Math.Min((decimal)preauthorizedLitres, tank.CurrentVolume);
            tank.CurrentVolume -= reserve;

            var session = new FuellingSessionEntity
            {
                Id = Guid.NewGuid().ToString(),
                PumpId = pump.Id,
                TankId = tank.Id,
                FuelType = fuelType,
                ReservedVolume = reserve,
                Status = SessionStatus.Reserved
            };
            
            db.FuellingSessions.Add(session);
            db.SaveChanges();
            return StartFuellingResult.Ok(session.Id, (double)reserve);
        }
    }

    public async Task<CompleteFuellingResult> CompleteFuellingAsync(string sessionId, double actualLitres)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = await db.FuellingSessions.FindAsync(sessionId);
        if (session == null)
            return CompleteFuellingResult.Fail(ErrorMessages.FuellingSessionNotFound);

        var pump = await db.Pumps.FindAsync(session.PumpId);
        if (pump == null)
        {
            db.FuellingSessions.Remove(session);
            await db.SaveChangesAsync();
            return CompleteFuellingResult.Fail(ErrorMessages.PumpNotFound);
        }
        
        var tank = await db.Tanks.FindAsync(session.TankId);
        if (tank == null)
        {
            pump.IsBusy = false;
            db.FuellingSessions.Remove(session);
            await db.SaveChangesAsync();
            return CompleteFuellingResult.Fail(ErrorMessages.TankNotFound);
        }

        if (session.Status != SessionStatus.Reserved)
        {
            pump.IsBusy = false;
            db.FuellingSessions.Remove(session);
            await db.SaveChangesAsync();
            return CompleteFuellingResult.Fail(ErrorMessages.SessionAlreadyCompleted);
        }
        
        lock (_lock)
        {
            decimal actual = Math.Min((decimal)actualLitres, session.ReservedVolume);
            decimal leftover = session.ReservedVolume - actual;
            tank.CurrentVolume += leftover;

            session.ActualVolume = actual;
            session.Status = SessionStatus.Completed;

            pump.IsBusy = false;

            db.FuellingSessions.Remove(session);
            db.SaveChanges();
            return CompleteFuellingResult.Ok();
        }
    }

    public async Task<AddFuelResult> AddFuelFastAsync(FuelType fuelType, double litres)
    {
        if (litres <= 0) return AddFuelResult.Fail(ErrorMessages.LitresMustBePositive);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var tank = await db.Tanks
            .Where(t => t.FuelType == fuelType)
            .OrderBy(t => t.CurrentVolume)
            .FirstOrDefaultAsync();
        if (tank == null)
            return AddFuelResult.Fail(ErrorMessages.NoTankForFuelType);
        
        lock (_lock)
        {
            var freeSpace = tank.Capacity - tank.CurrentVolume;
            var fuelToAdd = Math.Min((decimal)litres, freeSpace);
            tank.CurrentVolume += fuelToAdd;
            return AddFuelResult.Ok(tank.Id, (double)tank.CurrentVolume);
        }
    }

    public StartDeliveryResult StartDelivery(List<Compartment> compartments)
    {
        if (_isDeliveryInProgress)
            return StartDeliveryResult.Fail(ErrorMessages.DeliveryInProgress);
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        lock (_lock)
        {
            if (_isDeliveryInProgress)
                return StartDeliveryResult.Fail(ErrorMessages.DeliveryInProgress);
            
            _isDeliveryInProgress = true;
            var session = new DeliverySessionEntity
            {
                Id = Guid.NewGuid().ToString(),
                Compartments = compartments.Select(c => new DeliveryCompartmentEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    FuelType = c.FuelType,
                    Litres = c.Litres
                }).ToList()
            };
            db.DeliverySessions.Add(session);
            db.SaveChanges();
            return StartDeliveryResult.Ok(session.Id);
        }
    }

    public async Task<CompleteDeliveryResult> CompleteDeliveryAsync(string sessionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = await db.DeliverySessions
            .Include(s => s.Compartments)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null)
            return CompleteDeliveryResult.Fail(ErrorMessages.DeliverySessionNotFound);
        
        lock (_lock)
        {
            foreach (var compartment in session.Compartments)
            {
                var tanks = db.Tanks.Where(t => t.FuelType == compartment.FuelType).ToList();
                if (tanks.Count == 0)
                    continue;
                
                var fuelToAddForSingleTank = (decimal)compartment.Litres / tanks.Count;
                
                foreach (var tank in tanks)
                {
                    var freeSpace = tank.Capacity - tank.CurrentVolume;
                    var fuelToAdd = Math.Min(fuelToAddForSingleTank, freeSpace);
                    tank.CurrentVolume += fuelToAdd;
                }
            }
            
            db.DeliverySessions.Remove(session);
            db.SaveChanges();
            _isDeliveryInProgress = false;
            return CompleteDeliveryResult.Ok();
        }
    }
}