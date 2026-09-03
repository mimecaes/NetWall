using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using NetWall.IDE.Models;

namespace NetWall.IDE.Themes;

public static class ThemeManager
{
    public static void Apply(AppTheme theme)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var baseDictionary = app.Resources.MergedDictionaries.OfType<ResourceDictionary>().FirstOrDefault();
        if (baseDictionary is null)
        {
            return;
        }

        foreach (var (key, color) in ThemePalettes.GetColors(theme))
        {
            if (baseDictionary[key] is SolidColorBrush brush)
            {
                brush.Color = color;
            }
        }

        app.RequestedThemeVariant = theme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
    }
}
