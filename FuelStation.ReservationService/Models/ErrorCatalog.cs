namespace FuelStation.ReservationService.Models;

public static class ErrorCatalog
{
    // 1000–1999 Station
    public static readonly ErrorInfo StationClosedForFuelling = new(
        1001, "StationClosedForFuelling",
        "Station closed, delivery is in process. Fuelling rejected, desired fuel: {0}");

    public static readonly ErrorInfo StationClosedForDelivery = new(
        1002, "StationClosedForDelivery",
        "Station closed, delivery is in process. Another delivery is impossible");

    public static readonly ErrorInfo StationNotFound = new(
        1003, "StationNotFound",
        "Station not found");

    // 2000–2999 Pump
    public static readonly ErrorInfo PumpNotAutoSelected = new(
        2001, "PumpNotAutoSelected",
        "No suitable pump available. Desired fuel: {0}");

    public static readonly ErrorInfo PumpNotFound = new(
        2002, "PumpNotFound",
        "Pump {0} not found");

    public static readonly ErrorInfo PumpIsBusy = new(
        2003, "PumpIsBusy",
        "Pump {0} is busy");

    // 3000–3999 Tank / Fuel
    public static readonly ErrorInfo TankIsBusy = new(
        3001, "TankIsBusy",
        "Tank {0} is busy");

    public static readonly ErrorInfo TankNotFound = new(
        3002, "TankNotFound",
        "Tank {0} not found");

    public static readonly ErrorInfo NoFuelAvailable = new(
        3003, "NoFuelAvailable",
        "No fuel available in {0}");

    public static readonly ErrorInfo FuelTypeMismatch = new(
        3004, "FuelTypeMismatch",
        "Fuel type mismatch");

    // 4000–4999 Session
    public static readonly ErrorInfo FuellingSessionNotFound = new(
        4001, "FuellingSessionNotFound",
        "Fuelling session {0} not found");

    public static readonly ErrorInfo DeliverySessionNotFound = new(
        4002, "DeliverySessionNotFound",
        "Delivery session {0} not found");

    public static readonly ErrorInfo SessionAlreadyCompleted = new(
        4003, "SessionAlreadyCompleted",
        "Fuelling session {0} is already completed");

    // 5000–5999 Idempotency
    public static readonly ErrorInfo IdempotencyKeyNotProvidedForFuelling = new(
        5001, "IdempotencyKeyNotProvidedForFuelling",
        "Idempotency key not provided for fuelling");

    public static readonly ErrorInfo IdempotencyKeyNotProvidedForDelivering = new(
        5002, "IdempotencyKeyNotProvidedForDelivering",
        "Idempotency key not provided for delivering");

    public static readonly ErrorInfo IdempotencyConflict = new(
        5003, "IdempotencyConflict",
        "Idempotency conflict");
    
    // extra
    public static readonly ErrorInfo Unknown = new(
        9999, "Unknown",
        "Unknown error");
}