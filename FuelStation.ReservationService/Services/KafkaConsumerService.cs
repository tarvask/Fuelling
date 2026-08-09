using System.Text.Json;
using Confluent.Kafka;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.Shared.Constants;

namespace FuelStation.ReservationService.Services;

public class KafkaConsumerService : BackgroundService
{
    private readonly string _bootstrapServers;

    public KafkaConsumerService(KafkaConfigurationProvider kafkaConfigProvider)
    {
        Console.WriteLine($"[KafkaConsumerService] Kafka bootstrap servers: {kafkaConfigProvider.BootstrapServers}");
        _bootstrapServers = kafkaConfigProvider.BootstrapServers;
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = KafkaGroups.ReservationServiceLogger,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(new[]
        {
            KafkaTopics.FuellingStarted,
            KafkaTopics.FuellingCompleted,
            KafkaTopics.DeliveryEvents,
        });

        return Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(stoppingToken);
                    var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cr.Message.Value);
                    var stationId = payload != null && payload.TryGetValue(KafkaMessageKeys.StationId, out var sid) ? sid.GetString() : "?";
                    Console.WriteLine($"[Kafka] Station {stationId} | {cr.Topic}: {cr.Message.Value}");
                }
                catch (OperationCanceledException) { break; }
            }
            consumer.Close();
        }, stoppingToken);
    }
}