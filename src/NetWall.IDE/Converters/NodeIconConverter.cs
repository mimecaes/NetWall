using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Lucide.Avalonia;

namespace NetWall.IDE.Converters;


public sealed class NodeIconConverter : IMultiValueConverter
{
    public static readonly NodeIconConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [bool isFolder, bool isExpanded, string name, ..])
        {
            return LucideIconKind.File;
        }

        if (isFolder)
        {
            return isExpanded ? LucideIconKind.FolderOpen : LucideIconKind.Folder;
        }

        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".json" => LucideIconKind.FileBraces,
            ".nwclase" => LucideIconKind.GraduationCap,
            ".md" or ".txt" => LucideIconKind.FileText,
            ".nw" or ".cs" or ".xaml" or ".axaml" or ".xml"
                or ".yml" or ".yaml" or ".py" or ".js" or ".ts" or ".html" or ".css" => LucideIconKind.FileCode,
            _ => LucideIconKind.File,
        };
    }
}
