namespace FuelStation.ReservationService.Constants;

public static class ErrorMessages
{
    public const string StationClosedFuellingRejected = "Station {0} closed, delivery is in process. Fuelling rejected, desired fuel: {1}";
    public const string StationClosedDeliveryRejected = "Station {0} closed, delivery is in process. Another delivery is impossible";
    public const string PumpNotAutoSelected = "No suitable pump available. Desired fuel: {0}";
    public const string PumpNotFound = "Pump {0} not found";
    public const string PumpIsBusy = "Pump {0} is busy";
    public const string TankIsBusy = "Tank {0} is busy";
    public const string FuelTypeMismatch = "Fuel type mismatch";
    public const string TankNotFound = "Tank {0} not found";
    public const string NoFuelAvailable = "No fuel available in {0}";
    public const string FuellingSessionNotFound = "Fuelling session {0} not found";
    public const string SessionAlreadyCompleted = "Fuelling session {0} is already completed";
    public const string LitresMustBePositive = "Litres must be positive";
    public const string NoTankForFuelType = "No tank for this fuel type";

    public const string StationNotFound = "Station {0} not found";
    public const string DeliveryInProgress = "Delivery in progress";
    public const string DeliverySessionNotFound = "Delivery session {0} not found";

    public const string IdempotencyKeyNotProvidedForFuelling = "Idempotency key not provided for fuelling";
    public const string IdempotencyKeyNotProvidedForDelivering = "Idempotency key not provided for delivering";
    public const string IdempotencyConflict = "Idempotency conflict";
}