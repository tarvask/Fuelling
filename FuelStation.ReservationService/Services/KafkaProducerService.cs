using Confluent.Kafka;
using System.Text.Json;
using FuelStation.ReservationService.Constants;

namespace FuelStation.ReservationService.Services;

public class KafkaProducerService
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducerService()
    {
        var config = new ProducerConfig { BootstrapServers = "localhost:9092" };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task SendStartedEvent(string sessionId, string pumpId, string fuelType, double reservedLitres)
    {
        var msg = new
        {
            session_id = sessionId,
            pump_id = pumpId,
            fuel_type = fuelType,
            reserved_litres = reservedLitres,
            timestamp = DateTime.UtcNow
        };
        await _producer.ProduceAsync(KafkaTopics.FuelingStarted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(msg) });
    }

    public async Task SendCompletedEvent(string sessionId, string fuelType, double actualLitres)
    {
        var msg = new
        {
            session_id = sessionId,
            fuel_type = fuelType,
            actual_litres = actualLitres,
            timestamp = DateTime.UtcNow
        };
        await _producer.ProduceAsync(KafkaTopics.FuelingCompleted,
            new Message<string, string> { Key = sessionId, Value = JsonSerializer.Serialize(msg) });
    }

    public async Task SendFuelAddedEvent(string tankId, string fuelType, double litres, double newVolume)
    {
        var msg = new
        {
            tank_id = tankId,
            fuel_type = fuelType,
            litres = litres,
            new_volume = newVolume,
            timestamp = DateTime.UtcNow
        };
        await _producer.ProduceAsync(KafkaTopics.FuelAdded,
            new Message<string, string> { Key = tankId, Value = JsonSerializer.Serialize(msg) });
    }
}