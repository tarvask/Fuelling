using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Fuel;
using FuelStation.Shared.Models;
using FuelStation.Shared.Utilities;
using FuelStation.SupplyServiceUI.Constants;
using FuelStation.SupplyServiceUI.Infrastructure;
using FuelStation.SupplyServiceUI.Models;
using FuelStation.SupplyServiceUI.Services;
using Grpc.Net.Client;

namespace FuelStation.SupplyServiceUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<StationViewModel> _stations = new();

    private readonly FuelReservation.FuelReservationClient _client;

    public MainViewModel(AppConfigProvider appConfigProvider, DeliveryEventBus eventBus)
    {
        var channel = GrpcChannel.ForAddress(appConfigProvider.GrpcAddress);
        _client = new FuelReservation.FuelReservationClient(channel);
        _ = LoadStationsAsync();
        
        eventBus.DeliveryEventReceived += OnEventBusOnDeliveryEventReceived;
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
                Status = DeliveryStatuses.NoActive
            });
        }
    }
    
    public async Task StartDeliveryAsync(StationViewModel station, TankerConfig tanker)
    {
        station.Status = DeliveryStatuses.Requested;

        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new StartDeliveryRequest
        {
            StationId = station.Id,
            Compartments =
            {
                tanker.Compartments.Select(c => new Compartment
                {
                    FuelType = c.FuelType,
                    Litres = c.Litres
                })
            },
            IdempotencyKey = idempotencyKey
        };
        StartDeliveryResponse? startReply = await GrpcRetryHelper.RetryGrpcCallAsync<StartDeliveryResponse>(() =>
            _client.StartDeliveryAsync(request).ResponseAsync);

        if (startReply == null || startReply.Success == false)
        {
            station.Status = $"Error: {startReply?.Error ?? DeliveryStatuses.NoServerAnswer}";
        }
    }
    
    private void OnEventBusOnDeliveryEventReceived(DeliveryEventMessage message)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var station = Stations.FirstOrDefault(s => s.Id == message.StationId);
            
            if (station == null) return;
            
            station.Status = message.DeliveryStatus switch
            {
                DeliveryStatusType.Scheduled => DeliveryStatuses.Scheduled,
                DeliveryStatusType.Arrived => DeliveryStatuses.Arrived,
                DeliveryStatusType.Completed => DeliveryStatuses.Completed,
                DeliveryStatusType.Failed => DeliveryStatuses.Failed,
                _ => DeliveryStatuses.NoActive
            };
        });
    }
}
