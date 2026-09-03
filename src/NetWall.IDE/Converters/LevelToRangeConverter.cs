using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace NetWall.IDE.Converters;

public sealed class LevelToRangeConverter : IValueConverter
{
    public static readonly LevelToRangeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int level && level > 0 ? Enumerable.Range(0, level).ToArray() : Array.Empty<int>();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
