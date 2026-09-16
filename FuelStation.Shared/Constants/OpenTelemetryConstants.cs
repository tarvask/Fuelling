namespace FuelStation.Shared.Constants;

public static class OpenTelemetryConstants
{
    public const string PaymentService = "PaymentService";
    public const string PaymentServiceSource = "PaymentService.Kafka";
    public const string PaymentServiceActivity = "ProcessFuelingCompleted";

    public const string Simulator = "Simulator";

    public const string ReservationService = "ReservationService";

    public static class BaggageKeys
    {
        public const string BaggagePrefix = "baggage.";
    
        public const string StationId = "station_id";
    }
    
    public static class SpanKeys
    {
        public const string SessionId = "session.id";
        public const string StationId = "station.id";
        public const string FuelType = "fuel.type";
        public const string FuelLitres = "fuel.litres";
    }
}