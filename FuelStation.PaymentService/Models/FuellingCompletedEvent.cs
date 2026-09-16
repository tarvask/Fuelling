using System.Text.Json.Serialization;
using FuelStation.Shared.Constants;

namespace FuelStation.PaymentService.Models;

public class FuelingCompletedEvent
{
    [JsonPropertyName(KafkaMessageKeys.StationId)]
    public string StationId { get; init; } = string.Empty;
    [JsonPropertyName(KafkaMessageKeys.SessionId)]
    public string SessionId { get; init; } = string.Empty;
    [JsonPropertyName(KafkaMessageKeys.FuelType)]
    public string FuelType { get; init; } = string.Empty;
    [JsonPropertyName(KafkaMessageKeys.ActualLitres)]
    public double Litres { get; init; }
}