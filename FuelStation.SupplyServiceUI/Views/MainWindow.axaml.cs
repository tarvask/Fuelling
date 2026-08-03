using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FuelStation.SupplyServiceUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FuelStation.SupplyServiceUI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    
    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        AvaloniaXamlLoader.Load(this);
        
        _viewModel = viewModel;
        DataContext = _viewModel;
        
        _serviceProvider = serviceProvider;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.LoadStationsAsync();
    }

    private async void OnDeliveryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not StationViewModel station) return;

        var dialog = _serviceProvider.GetRequiredService<TankerSelectionDialog>();
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