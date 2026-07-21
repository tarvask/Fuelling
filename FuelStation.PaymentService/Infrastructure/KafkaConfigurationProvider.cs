using Microsoft.Extensions.Configuration;

namespace FuelStation.PaymentService.Infrastructure;

public class KafkaConfigurationProvider
{
    private readonly string _bootstrapServers;

    public KafkaConfigurationProvider()
    {
        var basePath = AppContext.BaseDirectory;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        _bootstrapServers =
            Environment.GetEnvironmentVariable("Kafka__BootstrapServers")
            ?? configuration.GetValue<string>("Kafka:BootstrapServers")
            ?? "localhost:9092";
    }

    public string BootstrapServers => _bootstrapServers;
}