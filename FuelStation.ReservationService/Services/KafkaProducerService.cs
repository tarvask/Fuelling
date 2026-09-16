using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using System.Text.Json;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.Shared.Constants;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace FuelStation.ReservationService.Services;

public interface IKafkaProducerService
{
    Task SendFuellingStartedEvent(string stationId, string sessionId, string pumpId, string fuelType, double reservedLitres);
    Task SendFuellingCompletedEvent(string stationId, string sessionId, string fuelType, double actualLitres);
    Task SendDeliveryEvent(string stationId, string sessionId, string deliveryStatus);
}

public class KafkaProducerService : IKafkaProducerService
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private readonly IProducer<string, string> _producer;

    public KafkaProducerService(KafkaConfigurationProvider kafkaConfigProvider)
    {
        Console.WriteLine($"[KafkaProducerService] Kafka bootstrap servers: {kafkaConfigProvider.BootstrapServers}");
        var config = new ProducerConfig { BootstrapServers = kafkaConfigProvider.BootstrapServers };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task SendFuellingStartedEvent(string stationId, string sessionId, string pumpId, string fuelType, double reservedLitres)
    {
        var payload = new Dictionary<string,object>
        {
            { KafkaMessageKeys.StationId, stationId },
            { KafkaMessageKeys.SessionId, sessionId },
            { KafkaMessageKeys.PumpId, pumpId },
            { KafkaMessageKeys.FuelType, fuelType },
            { KafkaMessageKeys.ReservedLitres, reservedLitres },
            { KafkaMessageKeys.Timestamp, DateTime.UtcNow }
        };
        await _producer.ProduceAsync(KafkaTopics.FuellingStarted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(payload), Headers = GetTraceHeaders(stationId) });
    }

    public async Task SendFuellingCompletedEvent(string stationId, string sessionId, string fuelType, double actualLitres)
    {
        var payload = new Dictionary<string,object>
        {
            { KafkaMessageKeys.StationId, stationId },
            { KafkaMessageKeys.SessionId, sessionId },
            { KafkaMessageKeys.FuelType, fuelType },
            { KafkaMessageKeys.ActualLitres, actualLitres },
            { KafkaMessageKeys.Timestamp, DateTime.UtcNow }
        };
        await _producer.ProduceAsync(KafkaTopics.FuellingCompleted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(payload), Headers = GetTraceHeaders(stationId) });
    }

    public async Task SendDeliveryEvent(string stationId, string sessionId, string deliveryStatus)
    {
        var payload = new Dictionary<string,object> 
        {
            { KafkaMessageKeys.StationId, stationId },
            { KafkaMessageKeys.SessionId, sessionId },
            { KafkaMessageKeys.DeliveryStatus, deliveryStatus },
            { KafkaMessageKeys.Timestamp, DateTime.UtcNow }
        };
        await _producer.ProduceAsync(KafkaTopics.DeliveryEvents,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(payload), Headers = GetTraceHeaders(stationId) });
    }

    private static Headers GetTraceHeaders(string stationId)
    {
        var headers = new Headers();
        var activity = Activity.Current;
        if (activity != null)
        {
            // Add station_id to baggage so it propagates through the trace
            var baggage = Baggage.Current.SetBaggage(OpenTelemetryConstants.BaggageKeys.StationId, stationId);
            
            Propagator.Inject(
                new PropagationContext(activity.Context, baggage),
                headers,
                (carrier, key, value) => carrier.Add(key, Encoding.UTF8.GetBytes(value)));
        }
        return headers;
    }
}