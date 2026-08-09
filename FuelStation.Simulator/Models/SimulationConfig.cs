using Fuel;

namespace FuelStation.Simulator.Models;

public class SimulationConfig
{
    public int SpeedFactor { get; init; }
    public Dictionary<FuelType, double> FuelProbabilities { get; init; } = new();
    public int MinLitres { get; init; }
    public int MaxLitres { get; init; }
    public int MinIntervalVirtualMinutes { get; init; }
    public int MaxIntervalVirtualMinutes { get; init; }
    public int MinHumanFactorMinutes { get; init; }
    public int MaxHumanFactorMinutes { get; init; }
    public double PumpSpeedLitresPerMinute { get; init; }
}