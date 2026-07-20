namespace FuelStation.ReservationService.Constants;

public static class ErrorMessages
{
    public const string StationClosed = "Station closed for delivery. Desired fuel: {0}";
    public const string PumpNotAutoSelected = "No suitable pump available. Desired fuel: {0}";
    public const string PumpNotFound = "Pump not found";
    public const string PumpIsBusy = "Pump is busy";
    public const string TankIsBusy = "Tank is busy";
    public const string FuelTypeMismatch = "Fuel type mismatch";
    public const string TankNotFound = "Tank not found";
    public const string NoFuelAvailable = "No fuel available in {0}";
    public const string FuellingSessionNotFound = "Fuelling session not found";
    public const string SessionAlreadyCompleted = "Fuelling session is already completed";
    public const string LitresMustBePositive = "Litres must be positive";
    public const string NoTankForFuelType = "No tank for this fuel type";

    public const string DeliveryInProgress = "Delivery in progress";
    public const string DeliverySessionNotFound = "Delivery session not found";
}