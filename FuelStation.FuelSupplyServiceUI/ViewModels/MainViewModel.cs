using CommunityToolkit.Mvvm.ComponentModel;

namespace FuelStation.FuelSupplyServiceUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";
}
