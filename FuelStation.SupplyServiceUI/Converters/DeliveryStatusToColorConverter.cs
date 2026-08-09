using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using FuelStation.SupplyServiceUI.Constants;

namespace FuelStation.SupplyServiceUI.Converters;

public class DeliveryStatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                DeliveryStatuses.Scheduled => new SolidColorBrush(Colors.CornflowerBlue),
                DeliveryStatuses.Arrived  => new SolidColorBrush(Colors.Orange),
                DeliveryStatuses.Completed => new SolidColorBrush(Colors.LimeGreen),
                DeliveryStatuses.Failed => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}