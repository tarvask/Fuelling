namespace FuelStation.Shared.Models;

public class DeliveryEventMessage
{
    public string SessionId { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public DeliveryStatusType DeliveryStatus { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum DeliveryStatusType
{
    Scheduled = 0,
    Arrived = 1,
    Completed = 2,
    Failed = 3
}