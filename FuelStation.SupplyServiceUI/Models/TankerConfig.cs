using System.Collections.Generic;
using Fuel;

namespace FuelStation.SupplyServiceUI.Models;

public class TankersConfiguration
{
    public List<TankerConfig> Tankers { get; set; } = new();
}

public class TankerConfig
{
    public string Name { get; set; } = string.Empty;
    public List<CompartmentConfig> Compartments { get; set; } = new();
}

public class CompartmentConfig
{
    public FuelType FuelType { get; set; }
    public double Litres { get; set; }
}