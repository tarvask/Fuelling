using Grpc.Net.Client;
using Fuel;
using FuelStation.Simulator.Infrastructure;
using FuelStation.Simulator.Models;
using FuelStation.Simulator.Services;
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
var fuellingTaskRunner = new FuelingTaskRunner(simulationConfigProvider.StationId, simulationConfig, simulationConfigProvider);

Console.WriteLine("Simulator started. Press Ctrl+C to stop.");

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
    _ = Task.Run(async () => 
    {
        await fuellingTaskRunner.RunRandomFuellingAsync(client);
    });

    // Wait for the next car
    await Task.Delay(GetInterCarIntervalMs());
}

int GetInterCarIntervalMs()
{
    var virtualMinutes = Random.Shared.Next(simulationConfig.MinIntervalVirtualMinutes, simulationConfig.MaxIntervalVirtualMinutes + 1);
    return virtualMinutes * 60 * 1000 / simulationConfigProvider.SpeedFactor;
}