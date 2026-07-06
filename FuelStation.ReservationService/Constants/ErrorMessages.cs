namespace FuelStation.ReservationService.Constants;

public static class ErrorMessages
{
    public const string PumpNotFound = "Pump not found";
    public const string FuelTypeMismatch = "Fuel type mismatch";
    public const string TankNotFound = "Tank not found";
    public const string NoFuelAvailable = "No fuel available";
    public const string SessionNotFound = "Session not found";
    public const string SessionAlreadyCompleted = "Session already completed or cancelled";
    public const string LitresMustBePositive = "Litres must be positive";
    public const string NoTankForFuelType = "No tank for this fuel type";
}