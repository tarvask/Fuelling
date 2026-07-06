namespace FuelStation.ReservationService.Models;

public record StartFuelingResult
{
    public bool Success { get; init; }
    public string? SessionId { get; init; }
    public double ReservedLitres { get; init; }
    public string? Error { get; init; }

    public static StartFuelingResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static StartFuelingResult Ok(string sessionId, double reservedLitres) =>
        new() { Success = true, SessionId = sessionId, ReservedLitres = reservedLitres };
}

public record StopFuelingResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static StopFuelingResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static StopFuelingResult Ok() =>
        new() { Success = true };
}

public record AddFuelResult
{
    public bool Success { get; init; }
    public string? TankId { get; init; }
    public double NewVolume { get; init; }
    public string? Error { get; init; }

    public static AddFuelResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static AddFuelResult Ok(string tankId, double newVolume) =>
        new() { Success = true, TankId = tankId, NewVolume = newVolume };
}