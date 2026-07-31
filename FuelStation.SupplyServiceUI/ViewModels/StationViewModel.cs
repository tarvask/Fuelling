using CommunityToolkit.Mvvm.ComponentModel;

namespace FuelStation.SupplyServiceUI.ViewModels;

public partial class StationViewModel : ObservableObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    
    [ObservableProperty]
    private string _status = "No active delivery";
}