using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Net.Client;
using Fuel;
using FuelStation.Simulator.Infrastructure;
using FuelStation.Simulator.Models;

// ----- Configuration parsing (enum-compatible) -----
var configJson = File.ReadAllText("appsettings.json");
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
options.Converters.Add(new JsonStringEnumConverter());
var simConfig = JsonSerializer.Deserialize<SimulationConfig>(configJson, options)!;

var sim = simConfig.Simulation;
var pumps = sim.Pumps;
var probs = sim.FuelProbabilities;
var speedFactor = sim.SpeedFactor;
var minLitres = sim.MinLitres;
var maxLitres = sim.MaxLitres;
var minIntervalMin = sim.MinIntervalVirtualMinutes;
var maxIntervalMin = sim.MaxIntervalVirtualMinutes;

var rnd = new Random();

var handler = new HttpClientHandler
{
    // for local development without certificates
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
var httpClient = new HttpClient(handler)
{
    DefaultRequestVersion = new Version(2, 0),
    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
};

var grpcConfigProvider = new GrpcConfigProvider();
var channel = GrpcChannel.ForAddress(grpcConfigProvider.GrpcAddress, new GrpcChannelOptions
{
    HttpClient = httpClient
});

var client = new FuelReservation.FuelReservationClient(channel);

Console.WriteLine("Simulator started. Press Ctrl+C to stop.");

// ----- Local helper functions for timing -----
int GetTotalFuelingProcessDurationMs(double litres)
{
    return GetFuelingDurationFromVolumeMs(litres) + GetHumanFactorDurationMs();
}

int GetHumanFactorDurationMs()
{
    int virtualMinutes = rnd.Next(1, 5);                // 1–4 minutes of fueling
    return virtualMinutes * 60 * 1000 / speedFactor;    // convert to real milliseconds
}

int GetFuelingDurationFromVolumeMs(double litres)
{
    double pumpSpeed = simConfig.Simulation.PumpSpeedLitresPerMinute;
    int virtualMinutes = (int)Math.Ceiling(litres / pumpSpeed);
    return virtualMinutes * 60 * 1000 / speedFactor;
}

int GetInterCarIntervalMs()
{
    int virtualMinutes = rnd.Next(minIntervalMin, maxIntervalMin + 1);
    return virtualMinutes * 60 * 1000 / speedFactor;
}

// ----- Main simulation loop -----
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("Shutting down Simulator...");
};

while (!cts.Token.IsCancellationRequested)
{
    var fuelRequest = CreateRandomFuelData(probs, minLitres, maxLitres, rnd);

    try
    {
        var startReply = await client.StartFuellingAsync(new StartFuellingRequest
        {
            PumpId = string.Empty,
            FuelType = fuelRequest.FuelType,
            PreauthorizedLitres = fuelRequest.Litres
        });

        if (!startReply.Success)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Start failed: {startReply.Error}");
        }
        else
        {
            Console.WriteLine($"[{DateTime.Now:T}] Session {startReply.SessionId}: reserved {startReply.ReservedLitres}L {fuelRequest.FuelType}");

            // Simulate fueling time
            await Task.Delay(GetTotalFuelingProcessDurationMs(fuelRequest.Litres));

            var stopReply = await client.CompleteFuellingAsync(new CompleteFuellingRequest
            {
                SessionId = startReply.SessionId,
                FuelType = fuelRequest.FuelType,
                ActualLitres = startReply.ReservedLitres
            });

            Console.WriteLine(stopReply.Success
                ? $"[{DateTime.Now:T}] Session {startReply.SessionId} ended: actual {startReply.ReservedLitres}L, success={stopReply.Success}"
                : $"[{DateTime.Now:T}] Session {startReply.SessionId} ended: actual {startReply.ReservedLitres}L, success={stopReply.Success}, error={stopReply.Error}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }

    // Wait for the next car
    await Task.Delay(GetInterCarIntervalMs());
}

static FuelRequest CreateRandomFuelData(
    Dictionary<FuelType, double> probabilities, int minLitres, int maxLitres, Random rnd)
{
    double dice = rnd.NextDouble();
    double cumulative = 0;
    FuelType selectedFuel = probabilities.Keys.First();
    foreach (var kvp in probabilities)
    {
        cumulative += kvp.Value;
        if (dice <= cumulative) { selectedFuel = kvp.Key; break; }
    }

    double litres = rnd.Next(minLitres, maxLitres + 1);
    return new FuelRequest(selectedFuel, litres);
}

public record FuelRequest(FuelType FuelType, double Litres);