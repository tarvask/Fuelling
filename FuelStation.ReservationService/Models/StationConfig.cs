namespace FuelStation.ReservationService.Models;

public class StationConfig
{
    public List<TankConfig> Tanks { get; set; } = new();
    public List<PumpConfig> Pumps { get; set; } = new();
}

public class TankConfig
{
    public string Id { get; set; } = "";
    public string FuelType { get; set; } = "";
    public decimal Capacity { get; set; }
    public decimal CurrentVolume { get; set; }
}

public class PumpConfig
{
    public string Id { get; set; } = "";
    public string FuelType { get; set; } = "";
    public string TankId { get; set; } = "";
}