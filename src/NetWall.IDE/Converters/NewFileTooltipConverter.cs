using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using NetWall.IDE.Localization;

namespace NetWall.IDE.Converters;

public sealed class NewFileTooltipConverter : IMultiValueConverter
{
    public static readonly NewFileTooltipConverter Full = new(withShortcut: true);
    public static readonly NewFileTooltipConverter Short = new(withShortcut: false);

    private readonly bool _withShortcut;

    private NewFileTooltipConverter(bool withShortcut) => _withShortcut = withShortcut;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isFolderOpen = values.Count > 0 && values[0] is true;
        if (!isFolderOpen)
        {
            return LocalizationService.T("Tooltip.OpenFolderFirst");
        }

        return LocalizationService.T(_withShortcut ? "Tooltip.NewFileShortcut" : "Tooltip.NewFile");
    }
}
