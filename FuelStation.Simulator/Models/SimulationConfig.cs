using Fuel;

namespace FuelStation.Simulator.Models;

public class SimulationConfig
{
    public SimulationSection Simulation { get; init; } = new();
}

public class SimulationSection
{
    public int SpeedFactor { get; init; }
    public List<PumpInfo> Pumps { get; init; } = new();
    public Dictionary<FuelType, double> FuelProbabilities { get; init; } = new();
    public int MinLitres { get; init; }
    public int MaxLitres { get; init; }
    public int MinIntervalVirtualMinutes { get; init; }
    public int MaxIntervalVirtualMinutes { get; init; }
    public double PumpSpeedLitresPerMinute { get; init; }
}

public class PumpInfo
{
    public string Id { get; init; } = "";
    public List<FuelType> FuelTypes { get; init; } = new();
}