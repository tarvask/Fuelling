using System.Collections.Generic;
using Fuel;

namespace FuelStation.SupplyServiceUI.Models;

public class TankerConfig
{
    public string Name { get; init; } = string.Empty;
    public List<CompartmentConfig> Compartments { get; init; } = new();
}

public class CompartmentConfig
{
    public FuelType FuelType { get; init; }
    public double Litres { get; init; }
}