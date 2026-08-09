using Confluent.Kafka;
using System.Text.Json;
using Fuel;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.Shared.Constants;

namespace FuelStation.ReservationService.Services;

public class KafkaProducerService
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducerService(KafkaConfigurationProvider kafkaConfigProvider)
    {
        Console.WriteLine($"[KafkaProducerService] Kafka bootstrap servers: {kafkaConfigProvider.BootstrapServers}");
        var config = new ProducerConfig { BootstrapServers = kafkaConfigProvider.BootstrapServers };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task SendFuellingStartedEvent(string stationId, string sessionId, string pumpId, string fuelType, double reservedLitres)
    {
        var message = new Dictionary<string,object>
        {
            { KafkaMessageKeys.StationId, stationId },
            { KafkaMessageKeys.SessionId, sessionId },
            { KafkaMessageKeys.PumpId, pumpId },
            { KafkaMessageKeys.FuelType, fuelType },
            { KafkaMessageKeys.ReservedLitres, reservedLitres },
            { KafkaMessageKeys.Timestamp, DateTime.UtcNow }
        };
        await _producer.ProduceAsync(KafkaTopics.FuellingStarted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(message) });
    }

    public async Task SendFuellingCompletedEvent(string stationId, string sessionId, string fuelType, double actualLitres)
    {
        var message = new Dictionary<string,object>
        {
            { KafkaMessageKeys.StationId, stationId },
            { KafkaMessageKeys.SessionId, sessionId },
            { KafkaMessageKeys.FuelType, fuelType },
            { KafkaMessageKeys.ActualLitres, actualLitres },
            { KafkaMessageKeys.Timestamp, DateTime.UtcNow }
        };
        await _producer.ProduceAsync(KafkaTopics.FuellingCompleted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(message) });
    }

    public async Task SendFuelAddedFastEvent(string stationId, string tankId, string fuelType, double litres, double newVolume)
    {
        var message = new Dictionary<string,object>
        {
            { KafkaMessageKeys.StationId, stationId },
            { KafkaMessageKeys.TankId, tankId },
            { KafkaMessageKeys.FuelType, fuelType },
            { KafkaMessageKeys.Litres, litres },
            { KafkaMessageKeys.NewVolume, newVolume },
            { KafkaMessageKeys.Timestamp, DateTime.UtcNow }
        };
        await _producer.ProduceAsync(KafkaTopics.FuelAddedFast,
            new Message<string, string> { Key = tankId, Value = JsonSerializer.Serialize(message) });
    }

    public async Task SendDeliveryStartedEvent(string stationId, string sessionId, List<Compartment> compartments)
    {
        var message = new Dictionary<string,object>
        {
            { KafkaMessageKeys.StationId, stationId },
            { KafkaMessageKeys.SessionId, sessionId },
            { KafkaMessageKeys.Compartments, compartments.Select(c => new Dictionary<string,object>
            {
                { KafkaMessageKeys.FuelType, c.FuelType.ToString() },
                { KafkaMessageKeys.Litres, c.Litres }
            })},
            { KafkaMessageKeys.Timestamp, DateTime.UtcNow }
        };
        await _producer.ProduceAsync(KafkaTopics.DeliveryStarted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(message) });
    }

    public async Task SendDeliveryCompletedEvent(string stationId, string sessionId)
    {
        var message = new Dictionary<string,object> 
        { 
            { KafkaMessageKeys.StationId, stationId },
            { KafkaMessageKeys.SessionId, sessionId },
            { KafkaMessageKeys.Timestamp, DateTime.UtcNow }
        };
        await _producer.ProduceAsync(KafkaTopics.DeliveryCompleted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(message) });
    }
    
    public async Task SendDeliveryEvent(string stationId, string sessionId, string deliveryStatus)
    {
        var message = new Dictionary<string,object> 
        { 
            { KafkaMessageKeys.StationId, stationId },
            { KafkaMessageKeys.SessionId, sessionId },
            { KafkaMessageKeys.DeliveryStatus, deliveryStatus },
            { KafkaMessageKeys.Timestamp, DateTime.UtcNow }
        };
        await _producer.ProduceAsync(KafkaTopics.DeliveryEvents,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(message) });
    }
}