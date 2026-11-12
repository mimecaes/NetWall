using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NetWall.IDE.Converters;

public sealed class NewFileTooltipConverter : IValueConverter
{
    public static readonly NewFileTooltipConverter Instance = new();

    private const string DisabledMessage = "Abre una carpeta primero para crear un archivo";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter as string ?? string.Empty : DisabledMessage;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
