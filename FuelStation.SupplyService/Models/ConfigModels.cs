namespace FuelStation.FuelSupply.Models;

public class AppConfig
{
    public string GrpcAddress { get; init; } = "";
    public List<TankerConfig> TankerProfiles { get; init; } = new();
    public int DeliveryDelaySeconds { get; init; }
}

public class TankerConfig
{
    public List<CompartmentConfig> Compartments { get; init; } = new();
}

public class CompartmentConfig
{
    public string FuelType { get; init; } = "";
    public double Litres { get; init; }
}