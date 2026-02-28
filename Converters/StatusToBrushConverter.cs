using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using JobTracker.Models;

namespace JobTracker.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ApplicationStatus status)
            return Brushes.Black;

        return status switch
        {
            ApplicationStatus.Proceed => Brushes.ForestGreen,
            ApplicationStatus.Pending => Brushes.Goldenrod,
            ApplicationStatus.Rejected => Brushes.IndianRed,
            _ => Brushes.Black
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}