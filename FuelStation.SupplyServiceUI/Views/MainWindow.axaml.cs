using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using FuelStation.SupplyServiceUI.ViewModels;

namespace FuelStation.SupplyServiceUI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.LoadStationsAsync();
    }

    private async void OnDeliveryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not StationViewModel station) return;
        
        var dialog = new TankerSelectionDialog();
        var result = await dialog.ShowDialog<bool>(this);
        if (result)
        {
            var selectedTanker = ((TankerSelectionViewModel)dialog.DataContext!).SelectedTankerConfig;
            if (selectedTanker != null)
            {
                await _viewModel.StartDeliveryAsync(station, selectedTanker);
            }
        }
    }
}