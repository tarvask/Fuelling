using Confluent.Kafka;
using FuelStation.ReservationService.Infrastructure;
using FuelStation.Shared;

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
            GroupId = "reservation-service-logger",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(new[]
        {
            KafkaTopics.FuellingStarted,
            KafkaTopics.FuellingCompleted,
            KafkaTopics.FuelAddedFast,
            KafkaTopics.DeliveryStarted,
            KafkaTopics.DeliveryCompleted
        });

        return Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(stoppingToken);
                    Console.WriteLine($"[Kafka] {cr.Topic}: {cr.Message.Value}");
                }
                catch (OperationCanceledException) { break; }
            }
            consumer.Close();
        }, stoppingToken);
    }
}