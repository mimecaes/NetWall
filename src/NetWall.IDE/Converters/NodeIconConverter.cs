using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NetWall.IDE.Models;

namespace NetWall.IDE.Converters;

public sealed class NodeIconConverter : IValueConverter
{
    public static readonly NodeIconConverter Instance = new();

    private const string FolderData = "M1.5,4 L6,4 L7.2,5.5 L14.5,5.5 L14.5,13 L1.5,13 Z";
    private const string FileData = "M4,1.5 L9,1.5 L12.5,5 L12.5,14.5 L4,14.5 Z M9,1.5 L9,5 L12.5,5";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isFolder = value is FileTreeNode { IsFolder: true };
        return Geometry.Parse(isFolder ? FolderData : FileData);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
