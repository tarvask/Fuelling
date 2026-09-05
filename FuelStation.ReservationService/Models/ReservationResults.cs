using System.Text.Json.Serialization;

namespace FuelStation.ReservationService.Models;

public record StartFuellingResult
{
    public bool Success { get; init; }
    public string? SessionId { get; init; }
    public double ReservedLitres { get; init; }
    public string? Error { get; init; }
    
    // private constructor for Fail/Ok fabric methods
    private StartFuellingResult() { }

    // constructor to use with System.Text.Json
    [JsonConstructor]
    public StartFuellingResult(bool success, string? sessionId, double reservedLitres, string? error)
    {
        Success = success;
        SessionId = sessionId;
        ReservedLitres = reservedLitres;
        Error = error;
    }

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

public record StartDeliveryResult
{
    public bool Success { get; private init; }
    public string? SessionId { get; private init; }
    public string? Error { get; private init; }
    
    // private constructor for Fail/Ok fabric methods
    private StartDeliveryResult() {}
    
    // constructor to use with System.Text.Json
    [JsonConstructor]
    public StartDeliveryResult(bool success, string? sessionId, string? error)
    {
        Success = success;
        SessionId = sessionId;
        Error = error;
    }

    public static StartDeliveryResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static StartDeliveryResult Ok(string sessionId) =>
        new() { Success = true, SessionId = sessionId };
}