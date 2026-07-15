using Fuel;
using FuelStation.FuelSupply.Models;

namespace FuelStation.FuelSupply.Services;

public static class DeliveryService
{
    public static async Task StartDeliveryAsync(
        FuelReservation.FuelReservationClient client,
        List<TankerConfig> profiles,
        TimeSpan delay,
        Random rnd)
    {
        // Choose random profile
        var profile = profiles[rnd.Next(profiles.Count)];
        var compartments = profile.Compartments.Select(c => 
        {
            if (!Enum.TryParse<FuelType>(c.FuelType, ignoreCase: true, out var fuelType))
                throw new ArgumentException($"Invalid fuel type '{c.FuelType}' in tanker config");
            return new Compartment
            {
                FuelType = fuelType,
                Litres = c.Litres
            };
        }).ToList();
    
        Console.WriteLine($"[{DateTime.Now:T}] Sending delivery request...");
        var startReply = await client.StartDeliveryAsync(new StartDeliveryRequest
        {
            Compartments = { compartments }
        });
    
        if (!startReply.Success)
        {
            Console.WriteLine($"Delivery start failed: {startReply.Error}");
            return;
        }
    
        Console.WriteLine($"Delivery {startReply.SessionId} started. Waiting {delay.TotalSeconds} seconds...");
    
        // Countdown
        var remaining = delay;
        while (remaining > TimeSpan.Zero)
        {
            Console.Write($"\r{remaining.TotalSeconds:F0} seconds remaining...   ");
            await Task.Delay(200);
            remaining = remaining.Subtract(TimeSpan.FromSeconds(0.2));
        }
        Console.WriteLine();
    
        // Complete delivery
        var stopReply = await client.CompleteDeliveryAsync(new CompleteDeliveryRequest
        {
            SessionId = startReply.SessionId
        });
    
        Console.WriteLine(stopReply.Success
            ? $"Delivery {startReply.SessionId} completed."
            : $"Delivery completion failed: {stopReply.Error}");
    }
}