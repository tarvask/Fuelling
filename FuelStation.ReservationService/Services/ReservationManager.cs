using System.Collections.Concurrent;
using Fuel;
using FuelStation.ReservationService.Constants;
using FuelStation.ReservationService.Models;

namespace FuelStation.ReservationService.Services;

public class ReservationManager
{
    private readonly ConcurrentDictionary<string, TankState> _tanks = new();
    private readonly ConcurrentDictionary<string, PumpConfig> _pumps = new();
    private readonly ConcurrentDictionary<string, FuelSession> _sessions = new();
    private readonly object _lock = new();

    public ReservationManager(StationConfig config)
    {
        foreach (var t in config.Tanks)
            _tanks[t.Id] = new TankState
            {
                Id = t.Id,
                FuelType = Enum.Parse<FuelType>(t.FuelType),
                CurrentVolume = t.CurrentVolume
            };
        foreach (var p in config.Pumps)
            _pumps[p.Id] = p;
    }

    public StartFuelingResult StartFueling(string pumpId, FuelType fuelType, double preauthorizedLitres)
    {
        if (!_pumps.TryGetValue(pumpId, out var pump))
            return StartFuelingResult.Fail(ErrorMessages.PumpNotFound);

        if (!Enum.TryParse<FuelType>(pump.FuelType, out var pumpFuelType) || pumpFuelType != fuelType)
            return StartFuelingResult.Fail(ErrorMessages.FuelTypeMismatch);

        if (!_tanks.TryGetValue(pump.TankId, out var tank))
            return StartFuelingResult.Fail(ErrorMessages.TankNotFound);

        lock (_lock)
        {
            if (tank.CurrentVolume <= 0)
                return StartFuelingResult.Fail(ErrorMessages.NoFuelAvailable);

            decimal reserve = Math.Min((decimal)preauthorizedLitres, tank.CurrentVolume);
            tank.CurrentVolume -= reserve;

            var session = new FuelSession
            {
                Id = Guid.NewGuid().ToString(),
                PumpId = pumpId,
                FuelType = fuelType,
                TankId = tank.Id,
                ReservedVolume = reserve,
                Status = SessionStatus.Reserved
            };
            _sessions[session.Id] = session;

            return StartFuelingResult.Ok(session.Id, (double)reserve);
        }
    }

    public StopFuelingResult StopFueling(string sessionId, double actualLitres)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return StopFuelingResult.Fail(ErrorMessages.SessionNotFound);

        if (session.Status != SessionStatus.Reserved)
            return StopFuelingResult.Fail(ErrorMessages.SessionAlreadyCompleted);

        lock (_lock)
        {
            if (!_tanks.TryGetValue(session.TankId, out var tank))
                return StopFuelingResult.Fail(ErrorMessages.TankNotFound);

            decimal actual = Math.Min((decimal)actualLitres, session.ReservedVolume);
            decimal leftover = session.ReservedVolume - actual;
            tank.CurrentVolume += leftover;

            session.ActualVolume = actual;
            session.Status = SessionStatus.Completed;

            return StopFuelingResult.Ok();
        }
    }

    public AddFuelResult AddFuel(FuelType fuelType, double litres)
    {
        if (litres <= 0) return AddFuelResult.Fail(ErrorMessages.LitresMustBePositive);

        lock (_lock)
        {
            var target = FindSuitableTank(fuelType);

            if (target == null)
                return AddFuelResult.Fail(ErrorMessages.NoTankForFuelType);

            target.CurrentVolume += (decimal)litres;
            return AddFuelResult.Ok(target.Id, (double)target.CurrentVolume);
        }
    }

    // TODO: use service with pooled collection to find tank without allocation
    private TankState? FindSuitableTank(FuelType fuelType)
    {
        return _tanks.Values
            .Where(t => t.FuelType == fuelType)
            .OrderByDescending(t => t.CurrentVolume)
            .FirstOrDefault();
    }
}

public class TankState
{
    public string Id { get; set; } = "";
    public FuelType FuelType { get; set; }
    public decimal CurrentVolume { get; set; }
}

public class FuelSession
{
    public string Id { get; set; } = "";
    public string PumpId { get; set; } = "";
    public FuelType FuelType { get; set; }
    public string TankId { get; set; } = "";
    public decimal ReservedVolume { get; set; }
    public decimal? ActualVolume { get; set; }
    public string Status { get; set; } = "";
}