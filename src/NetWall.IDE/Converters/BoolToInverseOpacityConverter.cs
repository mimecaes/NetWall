using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NetWall.IDE.Converters;
public class BoolToInverseOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? 0.0 : 1.0;
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public static BoolToInverseOpacityConverter Instance { get; } = new();
}
