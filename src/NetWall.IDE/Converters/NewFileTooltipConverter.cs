using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NetWall.IDE.Converters;

public sealed class NewFileTooltipConverter : IValueConverter
{
    public static readonly NewFileTooltipConverter Instance = new();

    private const string DisabledMessage = "Open a folder first to create a file";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter as string ?? string.Empty : DisabledMessage;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
