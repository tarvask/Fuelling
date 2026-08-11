namespace FuelStation.ReservationService.Constants;

public static class IdempotencyConstants
{
    public const int IdempotencyKeyTtlHours = 24;
    public const int IdempotentResultRetryCount = 10;
    public const int IdempotentResultWaitDurationMs = 300;
    public const string ProcessingStatus = "processing";
}