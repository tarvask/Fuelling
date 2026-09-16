using Microsoft.Extensions.Configuration;

namespace FuelStation.PaymentService.Infrastructure;

public class KafkaConfigurationProvider
{
    public KafkaConfigurationProvider(IConfiguration configuration)
    {
        BootstrapServers =
            Environment.GetEnvironmentVariable("Kafka__BootstrapServers")
            ?? configuration.GetValue<string>("Kafka:BootstrapServers")
            ?? "localhost:9092";
    }

    public string BootstrapServers { get; }
}