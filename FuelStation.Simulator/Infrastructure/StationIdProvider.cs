using Microsoft.Extensions.Configuration;

namespace FuelStation.Simulator.Infrastructure;

public class StationIdProvider
{
    public StationIdProvider(IConfiguration configuration)
    {
        StationId =
            Environment.GetEnvironmentVariable("Simulation__StationId")
            ?? configuration.GetValue<string>("Simulation:StationId")
            ?? "LUK-01";
    }

    public string StationId { get; }
}