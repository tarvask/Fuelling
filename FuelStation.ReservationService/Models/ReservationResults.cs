using System.Text.Json.Serialization;

namespace FuelStation.ReservationService.Models;

public record StartFuellingResult
{
    public bool Success { get; init; }
    public string? SessionId { get; init; }
    public double ReservedLitres { get; init; }
    public int? ErrorNumericCode { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorText { get; init; }

    // private constructor for Fail/Ok fabric methods
    private StartFuellingResult() { }

    // constructor to use with System.Text.Json
    [JsonConstructor]
    public StartFuellingResult(bool success, string? sessionId, double reservedLitres, int? errorNumericCode, string? errorCode, string? errorText)
    {
        Success = success;
        SessionId = sessionId;
        ReservedLitres = reservedLitres;
        ErrorNumericCode = errorNumericCode;
        ErrorCode = errorCode;
        ErrorText = errorText;
    }
    
    public static StartFuellingResult Fail(string stationId, ErrorInfo error, params object[] args) =>
        new()
        {
            Success = false,
            
            ErrorNumericCode = error.NumericCode,
            ErrorCode = error.Code,
            ErrorText = $"[Station {stationId}] {error.Format(args)}"
        };

    public static StartFuellingResult Ok(string sessionId, double reservedLitres) =>
        new() { Success = true, SessionId = sessionId, ReservedLitres = reservedLitres };
}

public record CompleteFuellingResult
{
    public bool Success { get; private init; }
    public int? ErrorNumericCode { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorText { get; init; }

    public static CompleteFuellingResult Fail(string stationId, ErrorInfo error, params object[] args) =>
        new()
        {
            Success = false,
            
            ErrorNumericCode = error.NumericCode,
            ErrorCode = error.Code,
            ErrorText = $"[Station {stationId}] {error.Format(args)}"
        };

    public static CompleteFuellingResult Ok() =>
        new() { Success = true };
}

public record StartDeliveryResult
{
    public bool Success { get; private init; }
    public string? SessionId { get; private init; }
    public int? ErrorNumericCode { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorText { get; init; }
    
    // private constructor for Fail/Ok fabric methods
    private StartDeliveryResult() {}
    
    // constructor to use with System.Text.Json
    [JsonConstructor]
    public StartDeliveryResult(bool success, string? sessionId, int? errorNumericCode, string? errorCode, string? errorText)
    {
        Success = success;
        SessionId = sessionId;
        ErrorNumericCode = errorNumericCode;
        ErrorCode = errorCode;
        ErrorText = errorText;
    }

    public static StartDeliveryResult Fail(string stationId, ErrorInfo error, params object[] args) =>
        new()
        {
            Success = false,
            
            ErrorNumericCode = error.NumericCode,
            ErrorCode = error.Code,
            ErrorText = $"[Station {stationId}] {error.Format(args)}"
        };

    public static StartDeliveryResult Ok(string sessionId) =>
        new() { Success = true, SessionId = sessionId };
}