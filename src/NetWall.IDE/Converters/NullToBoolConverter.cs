using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NetWall.IDE.Converters;

public sealed class NullToBoolConverter : IValueConverter
{
    public static readonly NullToBoolConverter IsNotNull = new(true);
    public static readonly NullToBoolConverter IsNull = new(false);

    private readonly bool _returnTrueWhenNotNull;

    private NullToBoolConverter(bool returnTrueWhenNotNull)
    {
        _returnTrueWhenNotNull = returnTrueWhenNotNull;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is not null) == _returnTrueWhenNotNull;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
