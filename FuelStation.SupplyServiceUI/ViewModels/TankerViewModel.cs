using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FuelStation.SupplyServiceUI.Models;

namespace FuelStation.SupplyServiceUI.ViewModels;

public partial class TankerViewModel : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public List<CompartmentConfig> Compartments { get; set; } = new();

    public string Summary => string.Join(", ", Compartments.Select(c => $"{c.FuelType}: {c.Litres} л"));
}