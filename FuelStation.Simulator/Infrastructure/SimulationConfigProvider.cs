using Microsoft.Extensions.Configuration;

namespace FuelStation.Simulator.Infrastructure;

public class SimulationConfigProvider
{
    public SimulationConfigProvider(IConfiguration configuration)
    {
        StationId =
            Environment.GetEnvironmentVariable("Simulation__StationId")
            ?? configuration.GetValue<string>("Simulation:StationId")
            ?? "LUK-01";
        
        var speedFactorRaw = Environment.GetEnvironmentVariable("Simulation__SpeedFactor");
        SpeedFactor = int.TryParse(speedFactorRaw, out var speedFactor)
            ? speedFactor
            : configuration.GetValue<int>("Simulation:SpeedFactor");
    }

    public string StationId { get; }
    public int SpeedFactor { get; }
}