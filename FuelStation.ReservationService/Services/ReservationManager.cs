using System.Collections.Concurrent;
using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Models;

namespace FuelStation.ReservationService.Services;

public class ReservationManager
{
    private readonly ConcurrentDictionary<string, TankState> _tanks = new();
    private readonly ConcurrentDictionary<string, PumpState> _pumps = new();
    private readonly ConcurrentDictionary<string, FuellingSessionState> _sessions = new();
    private readonly object _lock = new();

    public ReservationManager(StationConfig config)
    {
        foreach (var t in config.Tanks)
            _tanks[t.Id] = new TankState
            {
                Id = t.Id,
                FuelType = Enum.Parse<FuelType>(t.FuelType),
                CurrentVolume = t.CurrentVolume,
                Capacity = t.Capacity
            };
        foreach (var p in config.Pumps)
            _pumps[p.Id] = new PumpState
            {
                Id = p.Id,
                Nozzles = p.Nozzles.ConvertAll(CreateNozzleState),
                IsBusy = false
            };
    }

    public StartFuellingResult StartFuelling(string pumpId, FuelType fuelType, double preauthorizedLitres)
    {

        PumpState? pump;
        
        if (string.IsNullOrEmpty(pumpId))
        {
            if (TryGetSuitablePump(fuelType, out pump) == false)
                return StartFuellingResult.Fail(ErrorMessages.PumpNotAutoSelected);
        }
        else
        {
            if (_pumps.TryGetValue(pumpId, out pump) == false)
                return StartFuellingResult.Fail(ErrorMessages.PumpNotFound);
            
            if (pump.IsBusy)
                return StartFuellingResult.Fail(ErrorMessages.PumpIsBusy);
        }

        var nozzle = pump.Nozzles.Find(nozzleState => nozzleState.FuelType == fuelType);
        if (nozzle == null)
            return StartFuellingResult.Fail(ErrorMessages.FuelTypeMismatch);

        if (_tanks.TryGetValue(nozzle.TankId, out var tank) == false)
            return StartFuellingResult.Fail(ErrorMessages.TankNotFound);
        
        if (tank.CurrentVolume <= 0)
            return StartFuellingResult.Fail(string.Format(ErrorMessages.NoFuelAvailable, tank.Id));
        
        lock (_lock)
        {
            // double check
            if (pump.IsBusy)
                return StartFuellingResult.Fail(ErrorMessages.PumpIsBusy);
            
            pump.IsBusy = true;
            decimal reserve = Math.Min((decimal)preauthorizedLitres, tank.CurrentVolume);
            tank.CurrentVolume -= reserve;

            var session = new FuellingSessionState
            {
                Id = Guid.NewGuid().ToString(),
                PumpId = pump.Id,
                FuelType = fuelType,
                TankId = tank.Id,
                ReservedVolume = reserve,
                Status = SessionStatus.Reserved
            };
            _sessions[session.Id] = session;

            return StartFuellingResult.Ok(session.Id, (double)reserve);
        }
    }

    public CompleteFuellingResult CompleteFuelling(string sessionId, double actualLitres)
    {
        if (_sessions.TryGetValue(sessionId, out var session) == false)
            return CompleteFuellingResult.Fail(ErrorMessages.FuellingSessionNotFound);

        if (_pumps.TryGetValue(session.PumpId, out var pump) == false)
            return CompleteFuellingResult.Fail(ErrorMessages.PumpNotFound);

        try
        {
            lock (_lock)
            {
                if (_tanks.TryGetValue(session.TankId, out var tank) == false)
                    return CompleteFuellingResult.Fail(ErrorMessages.TankNotFound);

                decimal actual = Math.Min((decimal)actualLitres, session.ReservedVolume);
                decimal leftover = session.ReservedVolume - actual;
                tank.CurrentVolume += leftover;
                
                session.ActualVolume = actual;
                session.Status = SessionStatus.Completed;
                
                _sessions.Remove(sessionId, out _);

                return CompleteFuellingResult.Ok();
            }
        }
        finally
        {
            pump.IsBusy = false;
        }
    }

    public AddFuelResult AddFuelFast(FuelType fuelType, double litres)
    {
        if (litres <= 0) return AddFuelResult.Fail(ErrorMessages.LitresMustBePositive);

        lock (_lock)
        {
            if (TryGetSuitableTank(fuelType, out var tank) == false)
                return AddFuelResult.Fail(ErrorMessages.NoTankForFuelType);

            var freeSpace = tank.Capacity - tank.CurrentVolume;
            var fuelToAdd = Math.Min((decimal)litres, freeSpace);
            tank.CurrentVolume += fuelToAdd;
            return AddFuelResult.Ok(tank.Id, (double)tank.CurrentVolume);
        }
    }

    {

        lock (_lock)
        {


        }
    }

    private bool TryGetSuitablePump(FuelType fuelType, out PumpState suitablePump)
    {
        foreach (var pump in _pumps.Values)
        {
            if (pump.IsBusy) continue;

            foreach (var nozzle in pump.Nozzles)
            {
                if (nozzle.FuelType != fuelType) continue;

                if (_tanks[nozzle.TankId].CurrentVolume > 0)
                {
                    suitablePump = pump;
                    return true;
                }
            }
        }

        suitablePump = null!;
        return false;
    }

    private bool TryGetSuitableTank(FuelType fuelType, out TankState suitableTank)
    {
        var success = false;

        var minCurrentVolume = decimal.MaxValue;
        foreach (var tank in _tanks.Values)
        {
            if (tank.FuelType != fuelType) continue;
            if (tank.CurrentVolume >= minCurrentVolume) continue;
            
            suitableTank = tank;
            minCurrentVolume = tank.CurrentVolume;
            success = true;
        }

        suitableTank = null!;
        return success;
    }
    
    private static NozzleState CreateNozzleState(NozzleConfig nozzleConfig)
    {
        return new NozzleState
        {
            FuelType = Enum.Parse<FuelType>(nozzleConfig.FuelType),
            TankId = nozzleConfig.TankId
        };
    }
}