using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelStation.SupplyServiceUI.Infrastructure;
using FuelStation.SupplyServiceUI.Models;

namespace FuelStation.SupplyServiceUI.ViewModels;

public partial class TankerSelectionViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<TankerViewModel> _tankers = new();

    [ObservableProperty]
    private TankerViewModel? _selectedTanker;

    public TankerConfig? SelectedTankerConfig { get; private set; }

    public Action<bool>? CloseAction { get; set; }

    private readonly AppConfigProvider _appConfigProvider;
    
    [RelayCommand]
    private void Send()
    {
        if (SelectedTanker == null)
        {
            CloseAction?.Invoke(true);
            return;
        }
        
        SelectedTankerConfig = new TankerConfig
        {
            Name = SelectedTanker.Name,
            Compartments = SelectedTanker.Compartments.Select(c => new CompartmentConfig
            {
                FuelType = c.FuelType,
                Litres = c.Litres
            }).ToList()
        };
        
        CloseAction?.Invoke(true);
    }
    
    [RelayCommand]
    private void Cancel()
    {
        SelectedTankerConfig = null;
        CloseAction?.Invoke(false);
    }
    
    public TankerSelectionViewModel(AppConfigProvider appConfigProvider)
    {
        _appConfigProvider = appConfigProvider;
        var tankers = _appConfigProvider.Tankers;

        foreach (var tanker in tankers)
        {
            Tankers.Add(new TankerViewModel
            {
                Name = tanker.Name,
                Compartments = tanker.Compartments
            });
        }
    }
}