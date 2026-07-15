using System.Text.Json;
using Grpc.Net.Client;
using Fuel;
using FuelStation.FuelSupply.Models;
using FuelStation.FuelSupply.Services;

// ----- Configuration -----
var configJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var appConfig = JsonSerializer.Deserialize<AppConfig>(configJson, jsonOptions)!;

var profiles = appConfig.TankerProfiles;
var deliveryDelay = TimeSpan.FromSeconds(appConfig.DeliveryDelaySeconds);

// ----- gRPC channel -----
var handler = new HttpClientHandler();
handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
var httpClient = new HttpClient(handler)
{
    DefaultRequestVersion = new Version(2, 0),
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
};
using var channel = GrpcChannel.ForAddress(appConfig.GrpcAddress, new GrpcChannelOptions { HttpClient = httpClient });
var client = new FuelReservation.FuelReservationClient(channel);

// ----- Graceful shutdown -----
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nShutting down Fuel Supply...");
};

var rnd = new Random();
Console.WriteLine("Fuel Supply ready. Press 'F' to start delivery, Ctrl+C to exit.");

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.F)
            {
                await DeliveryService.StartDeliveryAsync(client, profiles, deliveryDelay, rnd);
            }
        }
        else
        {
            await Task.Delay(100, cts.Token);
        }
    }
}
catch (OperationCanceledException) { }
finally
{
    Console.WriteLine("Fuel Supply stopped.");
}