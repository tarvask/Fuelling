using FuelStation.PaymentService.Infrastructure;
using FuelStation.PaymentService.Services;
using FuelStation.Shared.Constants;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var kafkaConfigProvider = new KafkaConfigurationProvider(configuration);
var otlpConfigProvider = new OtlpConfigurationProvider(configuration);

// Prices
var prices = configuration.GetSection("Prices").Get<Dictionary<string, double>>() 
             ?? new Dictionary<string, double>();

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddService(OpenTelemetryConstants.PaymentService))
    .AddSource(OpenTelemetryConstants.PaymentServiceSource)
    .AddOtlpExporter(exporterOptions => { exporterOptions.Endpoint = new Uri(otlpConfigProvider.Endpoint); })
    .Build();

Console.WriteLine($"PaymentService is listening to {KafkaTopics.FuellingCompleted}...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("Shutting down Payment Service...");
};

var paymentProcessingService = new PaymentProcessingService(prices);
var kafkaConsumerService = new KafkaConsumerService(kafkaConfigProvider, paymentProcessingService);

try
{
    await kafkaConsumerService.StartAsync(cts.Token);
    // block main thread, until Ctrl+C is received
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // normal termination
}
finally
{
    await kafkaConsumerService.StopAsync(CancellationToken.None);
}