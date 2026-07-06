using Confluent.Kafka;
using System.Text.Json;
using System.Text.Json.Serialization;

var config = new ConsumerConfig
{
    BootstrapServers = "localhost:9092",
    GroupId = "payment-service",
    AutoOffsetReset = AutoOffsetReset.Earliest
};

var configJson = File.ReadAllText("appsettings.json");
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
options.Converters.Add(new JsonStringEnumConverter());
var pricesConfig = JsonSerializer.Deserialize<PricesConfig>(configJson, options)!;

using var consumer = new ConsumerBuilder<string, string>(config).Build();
consumer.Subscribe("fueling-completed");

Console.WriteLine("PaymentService is listening to fueling‑completed...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("Shutting down Payment Service...");
};

while (!cts.Token.IsCancellationRequested)
{
    try
    {
        var cr = consumer.Consume();
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cr.Message.Value);
        var sessionId = payload!["session_id"].GetString();
        var fuelType = payload["fuel_type"].GetString();
        var litres = payload["actual_litres"].GetDouble();
        if (fuelType != null && pricesConfig.Prices.TryGetValue(fuelType, out var fuelPrice))
        {
            var price = litres * fuelPrice;
            Console.WriteLine($">>> BILL: session {sessionId}, {litres:F1}L of {fuelType}. Paid {price:F1}.");
        }
        else
        {
            Console.WriteLine($">>> ERROR: bad fuelType {fuelType} in session {sessionId}.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"PaymentService error: {ex.Message}");
    }
}

public class PricesConfig
{
    public Dictionary<string, double> Prices { get; set; } = new();
}