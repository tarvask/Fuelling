namespace FuelStation.ReservationService.Models;

public class FuelNetworkConfig
{
    public List<StationConfig> Stations { get; init; } = new();
}

public class StationConfig
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Address { get; init; } = "";
    public List<TankConfig> Tanks { get; init; } = new();
    public List<PumpConfig> Pumps { get; init; } = new();
}

public class TankConfig
{
    public string Id { get; init; } = "";
    public string FuelType { get; init; } = "";
    public decimal Capacity { get; init; }
    public decimal CurrentVolume { get; init; }
}

public class PumpConfig
{
    public string Id { get; init; } = "";
    public List<NozzleConfig> Nozzles { get; init; } = new();
}

public class NozzleConfig
{
    public string FuelType { get; init; } = "";
    public string TankId { get; init; } = "";
}