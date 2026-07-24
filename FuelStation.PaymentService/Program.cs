using System.Globalization;
using Confluent.Kafka;
using System.Text.Json;
using FuelStation.PaymentService.Infrastructure;
using FuelStation.PaymentService.Models;
using FuelStation.Shared;

var kafkaConfigProvider = new KafkaConfigurationProvider();
var config = new ConsumerConfig
{
    BootstrapServers = kafkaConfigProvider.BootstrapServers,
    GroupId = "payment-service",
    AutoOffsetReset = AutoOffsetReset.Earliest
};

var configJson = File.ReadAllText("appsettings.json");
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var pricesConfig = JsonSerializer.Deserialize<PricesConfig>(configJson, options)!;

using var consumer = new ConsumerBuilder<string, string>(config).Build();
consumer.Subscribe(KafkaTopics.FuellingCompleted);

Console.WriteLine($"PaymentService is listening to {KafkaTopics.FuellingCompleted}...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("Shutting down Payment Service...");
};

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            var cr = consumer.Consume(cts.Token);
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cr.Message.Value);
            var stationId = payload!["station_id"].GetString();
            var sessionId = payload!["session_id"].GetString();
            var fuelType = payload["fuel_type"].GetString();
            var litres = payload["actual_litres"].GetDouble();
            if (fuelType != null && pricesConfig.Prices.TryGetValue(fuelType, out var fuelPrice))
            {
                var price = litres * fuelPrice;
                Console.WriteLine($">>> BILL: station {stationId}, session {sessionId}, {litres.ToString("F1", CultureInfo.InvariantCulture)}L of {fuelType}. Paid {price.ToString("F1", CultureInfo.InvariantCulture)}.");
            }
            else
            {
                Console.WriteLine($">>> ERROR: bad fuelType {fuelType} in session {sessionId} at station {stationId}.");
            }
        }
        catch (OperationCanceledException)
        {
            // normal termination
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PaymentService error: {ex.Message}");
        }
    }
}
catch (OperationCanceledException)
{
    // normal termination
}
finally
{
    consumer.Close();
}