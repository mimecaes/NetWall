using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NetWall.IDE.Converters;

public sealed class PathToBitmapConverter : IValueConverter
{
    public static readonly PathToBitmapConverter Instance = new();

    private readonly Dictionary<string, Bitmap> _bitmaps = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (!_bitmaps.TryGetValue(path, out var bitmap))
        {
            using var stream = AssetLoader.Open(new Uri(path));
            bitmap = new Bitmap(stream);
            _bitmaps[path] = bitmap;
        }

        return bitmap;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
