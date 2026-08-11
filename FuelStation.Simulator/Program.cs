using Grpc.Net.Client;
using Fuel;
using FuelStation.Shared.Constants;
using FuelStation.Shared.Utilities;
using FuelStation.Simulator.Infrastructure;
using FuelStation.Simulator.Models;
using Microsoft.Extensions.Configuration;

var basePath = AppContext.BaseDirectory;
var configuration = new ConfigurationBuilder()
    .SetBasePath(basePath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var simulationConfig = configuration.GetSection("Simulation").Get<SimulationConfig>();
if (simulationConfig == null)
{
    Console.WriteLine("Error: simulation section not set in appsettings.json");
    Console.WriteLine("Terminating...");
    return;
}

var simulationConfigProvider = new SimulationConfigProvider(configuration);
var stationId = simulationConfigProvider.StationId;

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
    int virtualMinutes = rnd.Next(simulationConfig.MinHumanFactorMinutes, simulationConfig.MaxHumanFactorMinutes + 1);
    return virtualMinutes * 60 * 1000 / simulationConfigProvider.SpeedFactor;
}

int GetFuelingDurationFromVolumeMs(double litres)
{
    double pumpSpeed = simulationConfig.PumpSpeedLitresPerMinute;
    int virtualMinutes = (int)Math.Ceiling(litres / pumpSpeed);
    return virtualMinutes * 60 * 1000 / simulationConfigProvider.SpeedFactor;
}

int GetInterCarIntervalMs()
{
    int virtualMinutes = rnd.Next(simulationConfig.MinIntervalVirtualMinutes, simulationConfig.MaxIntervalVirtualMinutes + 1);
    return virtualMinutes * 60 * 1000 / simulationConfigProvider.SpeedFactor;
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
    var fuelRequest = CreateRandomFuelData(simulationConfig.FuelProbabilities, stationId, simulationConfig.MinLitres, simulationConfig.MaxLitres, rnd);

    try
    {
        await FuelSingleCar(client, fuelRequest);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }

    // Wait for the next car
    await Task.Delay(GetInterCarIntervalMs());
}

static FuelRequest CreateRandomFuelData(
    Dictionary<FuelType, double> probabilities, string stationId, int minLitres, int maxLitres, Random rnd)
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
    return new FuelRequest(stationId, selectedFuel, litres);
}

async Task FuelSingleCar(FuelReservation.FuelReservationClient fuelReservationClient, FuelRequest fuelRequest)
{
    var idempotencyKey = Guid.NewGuid().ToString();
    var startFuellingRequest = new StartFuellingRequest
    {
        StationId = fuelRequest.StationId,
        PumpId = string.Empty,
        FuelType = fuelRequest.FuelType,
        PreauthorizedLitres = fuelRequest.Litres,
        IdempotencyKey = idempotencyKey
    };
    StartFuellingResponse? startReply = await GrpcRetryHelper.RetryGrpcCallAsync<StartFuellingResponse>(() =>
        fuelReservationClient.StartFuellingAsync(startFuellingRequest).ResponseAsync);

    if (startReply == null || startReply.Success == false)
    {
        Console.WriteLine($"[{DateTime.Now:T}] Start failed: {startReply?.Error ?? "No server answer"}");
        return;
    }

    Console.WriteLine($"[{DateTime.Now:T}] Session {startReply.SessionId}: reserved {startReply.ReservedLitres}L {fuelRequest.FuelType}");

    // Simulate fueling time
    await Task.Delay(GetTotalFuelingProcessDurationMs(fuelRequest.Litres));

    var completeFuellingRequest = new CompleteFuellingRequest
    {
        StationId = fuelRequest.StationId,
        SessionId = startReply.SessionId,
        FuelType = fuelRequest.FuelType,
        ActualLitres = startReply.ReservedLitres
    };
    CompleteFuellingResponse? completeReply = await GrpcRetryHelper.RetryGrpcCallAsync<CompleteFuellingResponse>(() =>
        fuelReservationClient.CompleteFuellingAsync(completeFuellingRequest).ResponseAsync);

    if (completeReply == null || completeReply.Success == false)
    {
        Console.WriteLine($"[{DateTime.Now:T}] Complete of session {startReply.SessionId} failed: {completeReply?.Error ?? "No server answer"}");
        return;
    }

    Console.WriteLine(completeReply.Success
        ? $"[{DateTime.Now:T}] Session {startReply.SessionId} ended: actual {startReply.ReservedLitres}L, success={completeReply.Success}"
        : $"[{DateTime.Now:T}] Session {startReply.SessionId} ended: actual {startReply.ReservedLitres}L, success={completeReply.Success}, error={completeReply.Error}");
}

public record FuelRequest(string StationId, FuelType FuelType, double Litres);