using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace NetWall.IDE.Converters;

public sealed class NodeKindConverter : IValueConverter
{
    public static readonly NodeKindConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Path.GetExtension(value as string ?? string.Empty).ToLowerInvariant() switch
        {
            ".nw" => "nw",
            ".nwclase" => "class",
            ".json" or ".cs" or ".xaml" or ".axaml" or ".xml" or ".yml" or ".yaml"
                or ".py" or ".js" or ".ts" or ".html" or ".css" => "code",
            _ => "plain",
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
