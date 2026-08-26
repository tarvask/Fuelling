namespace FuelStation.ReservationService.Models;

public record StartFuellingResult
{
    public bool Success { get; set; }
    public string? SessionId { get; set; }
    public double ReservedLitres { get; set; }
    public string? Error { get; set; }

    public static StartFuellingResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static StartFuellingResult Ok(string sessionId, double reservedLitres) =>
        new() { Success = true, SessionId = sessionId, ReservedLitres = reservedLitres };
}

public record CompleteFuellingResult
{
    public bool Success { get; private init; }
    public string? Error { get; private init; }

    public static CompleteFuellingResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static CompleteFuellingResult Ok() =>
        new() { Success = true };
}

public record AddFuelResult
{
    public bool Success { get; private init; }
    public string? TankId { get; private init; }
    public double NewVolume { get; private init; }
    public string? Error { get; private init; }

    public static AddFuelResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static AddFuelResult Ok(string tankId, double newVolume) =>
        new() { Success = true, TankId = tankId, NewVolume = newVolume };
}

public record StartDeliveryResult
{
    public bool Success { get; private init; }
    public string? SessionId { get; private init; }
    public string? Error { get; private init; }

    public static StartDeliveryResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static StartDeliveryResult Ok(string sessionId) =>
        new() { Success = true, SessionId = sessionId };
}

public record CompleteDeliveryResult
{
    public bool Success { get; private init; }
    public string? Error { get; private init; }

    public static CompleteDeliveryResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static CompleteDeliveryResult Ok() =>
        new() { Success = true };
}