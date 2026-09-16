using System.Globalization;
using FuelStation.PaymentService.Models;

namespace FuelStation.PaymentService.Services;

public class PaymentProcessingService
{
    private readonly Dictionary<string, double> _prices;

    public PaymentProcessingService(Dictionary<string, double> prices)
    {
        _prices = prices;
    }

    public void ProcessPayment(FuelingCompletedEvent evt)
    {
        if (!_prices.TryGetValue(evt.FuelType, out var fuelPrice))
        {
            Console.WriteLine($">>> ERROR: unknown fuel type {evt.FuelType}");
            return;
        }

        var price = evt.Litres * fuelPrice;
        Console.WriteLine(
            $">>> BILL: station {evt.StationId}, session {evt.SessionId}, " +
            $"{evt.Litres.ToString("F1", CultureInfo.InvariantCulture)}L of {evt.FuelType}. " +
            $"Paid {price.ToString("F1", CultureInfo.InvariantCulture)}.");
    }
}
