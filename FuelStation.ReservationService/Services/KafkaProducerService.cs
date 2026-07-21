using Confluent.Kafka;
using System.Text.Json;
using Fuel;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.Shared;

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

    public async Task SendFuellingStartedEvent(string sessionId, string pumpId, string fuelType, double reservedLitres)
    {
        var msg = new
        {
            session_id = sessionId,
            pump_id = pumpId,
            fuel_type = fuelType,
            reserved_litres = reservedLitres,
            timestamp = DateTime.UtcNow
        };
        await _producer.ProduceAsync(KafkaTopics.FuellingStarted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(msg) });
    }

    public async Task SendFuellingCompletedEvent(string sessionId, string fuelType, double actualLitres)
    {
        var msg = new
        {
            session_id = sessionId,
            fuel_type = fuelType,
            actual_litres = actualLitres,
            timestamp = DateTime.UtcNow
        };
        await _producer.ProduceAsync(KafkaTopics.FuellingCompleted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(msg) });
    }

    public async Task SendFuelAddedFastEvent(string tankId, string fuelType, double litres, double newVolume)
    {
        var msg = new
        {
            tank_id = tankId,
            fuel_type = fuelType,
            litres = litres,
            new_volume = newVolume,
            timestamp = DateTime.UtcNow
        };
        await _producer.ProduceAsync(KafkaTopics.FuelAddedFast,
            new Message<string, string> { Key = tankId, Value = JsonSerializer.Serialize(msg) });
    }

    public async Task SendDeliveryStartedEvent(string sessionId, List<Compartment> compartments)
    {
        var msg = new
        {
            session_id = sessionId,
            compartments = compartments.Select(c => new
            {
                fuel_type = c.FuelType.ToString(),
                litres = c.Litres
            }),
            timestamp = DateTime.UtcNow
        };
        await _producer.ProduceAsync(KafkaTopics.DeliveryStarted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(msg) });
    }

    public async Task SendDeliveryCompletedEvent(string sessionId)
    {
        var msg = new
        {
            session_id = sessionId,
            timestamp = DateTime.UtcNow
        };
        await _producer.ProduceAsync(KafkaTopics.DeliveryCompleted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(msg) });
    }
}