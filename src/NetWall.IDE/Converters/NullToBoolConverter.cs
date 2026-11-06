using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NetWall.IDE.Converters;

public sealed class NullToBoolConverter : IValueConverter
{
    public static readonly NullToBoolConverter IsNotNull = new();

    private NullToBoolConverter()
    {
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
