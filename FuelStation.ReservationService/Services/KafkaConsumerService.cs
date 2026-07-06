using Confluent.Kafka;
using FuelStation.ReservationService.Constants;

namespace FuelStation.ReservationService.Services;

public class KafkaConsumerService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = "reservation-service-logger",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(new[] { KafkaTopics.FuelingStarted, KafkaTopics.FuelingCompleted, KafkaTopics.FuelAdded });

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