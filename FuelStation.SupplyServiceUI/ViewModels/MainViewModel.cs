using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Fuel;
using FuelStation.SupplyServiceUI.Infrastructure;
using FuelStation.SupplyServiceUI.Models;
using Grpc.Net.Client;

namespace FuelStation.SupplyServiceUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<StationViewModel> _stations = new();

    private readonly FuelReservation.FuelReservationClient _client;

    public MainViewModel(AppConfigProvider appConfigProvider)
    {
        var channel = GrpcChannel.ForAddress(appConfigProvider.GrpcAddress);
        _client = new FuelReservation.FuelReservationClient(channel);
        _ = LoadStationsAsync();
    }
    
    public async Task LoadStationsAsync()
    {
        var response = await _client.GetStationsAsync(new GetStationsRequest());
        Stations.Clear();
        foreach (var station in response.Stations)
        {
            Stations.Add(new StationViewModel
            {
                Id = station.Id,
                Name = station.Name,
                Address = station.Address,
                Status = "No active delivery"
            });
        }
    }
    
    public async Task StartDeliveryAsync(StationViewModel station, TankerConfig tanker)
    {
        station.Status = "Waiting for tanker";
        
        var startReply = await _client.StartDeliveryAsync(new StartDeliveryRequest
        {
            StationId = station.Id,
            Compartments = { tanker.Compartments.Select(c => new Compartment
            {
                FuelType = c.FuelType,
                Litres = c.Litres
            }) }
        });

        if (!startReply.Success)
        {
            station.Status = $"Error: {startReply.Error}";
            return;
        }

        station.Status = "Unloading fuel...";
        await Task.Delay(TimeSpan.FromSeconds(5));
        station.Status = "Delivered";
    }
}
