using System;
using FuelStation.Shared.Models;

namespace FuelStation.SupplyServiceUI.Services;

public class DeliveryEventBus
{
    public event Action<DeliveryEventMessage>? DeliveryEventReceived;

    public void Publish(DeliveryEventMessage message)
    {
        DeliveryEventReceived?.Invoke(message);
    }
}