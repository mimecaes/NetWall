using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NetWall.IDE.Localization;

/// <summary>Looks up the localized string named by ConverterParameter. Bound to LocalizationService.Language
/// purely as a change trigger — the actual value comes from the parameter, not the bound Language.</summary>
public sealed class LocConverter : IValueConverter
{
    public static readonly LocConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        LocalizationService.T(parameter as string ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
