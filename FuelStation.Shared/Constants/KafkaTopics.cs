namespace FuelStation.Shared.Constants;

public static class KafkaTopics
{
    public const string FuellingStarted = "fuelling-started";
    public const string FuellingCompleted = "fuelling-completed";
    public const string FuelAddedFast = "fuel-added-fast";
    public const string DeliveryStarted = "delivery-started";
    public const string DeliveryCompleted = "delivery-completed";
    public const string DeliveryEvents = "delivery-events";
}