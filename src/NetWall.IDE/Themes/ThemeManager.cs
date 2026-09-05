using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using NetWall.IDE.Models;

namespace NetWall.IDE.Themes;

public static class ThemeManager
{
    private static readonly string[] ColorKeys =
    [
        "Background", "PanelBackground", "SurfaceBackground", "Accent", "Accent2",
        "TextPrimary", "TextMuted", "TextDisabled", "Border",
        "Hover", "Active", "RunGreen", "StopRed",
        "TabActiveBackground", "TabInactiveBackground", "TerminalBackground", "EditorBackground",
    ];

    private static DispatcherTimer? _topBarFadeTimer;

    public static void Apply(AppTheme theme)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var dictionaries = app.Resources.MergedDictionaries.OfType<ResourceDictionary>().ToList();
        var baseDictionary = dictionaries.FirstOrDefault(d => d.ContainsKey("BackgroundBrush"));
        var prefix = theme.ToString();
        var themeDictionary = dictionaries.FirstOrDefault(d => d.ContainsKey($"{prefix}.Background"));

        if (baseDictionary is null || themeDictionary is null)
        {
            return;
        }

        foreach (var key in ColorKeys)
        {
            if (themeDictionary[$"{prefix}.{key}"] is Color color && baseDictionary[$"{key}Brush"] is SolidColorBrush brush)
            {
                brush.Color = color;
            }
        }

        if (baseDictionary["TopBarBackgroundBrush"] is LinearGradientBrush topBar)
        {
            var targets = new Color[topBar.GradientStops.Count];
            for (var i = 0; i < topBar.GradientStops.Count; i++)
            {
                targets[i] = themeDictionary[$"{prefix}.TopBarStop{i}"] is Color stopColor
                    ? stopColor
                    : topBar.GradientStops[i].Color;
            }

            AnimateGradientStops(topBar, targets);
        }


        app.RequestedThemeVariant = ThemeVariant.Dark;
    }

    private static void AnimateGradientStops(LinearGradientBrush brush, Color[] targets)
    {
        _topBarFadeTimer?.Stop();

        var starts = brush.GradientStops.Select(s => s.Color).ToArray();
        var startedAt = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(400);

        _topBarFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _topBarFadeTimer.Tick += (_, _) =>
        {
            var t = Math.Min(1.0, (DateTime.UtcNow - startedAt).TotalMilliseconds / duration.TotalMilliseconds);
            for (var i = 0; i < brush.GradientStops.Count; i++)
            {
                brush.GradientStops[i].Color = Lerp(starts[i], targets[i], t);
            }

            if (t >= 1.0)
            {
                _topBarFadeTimer?.Stop();
            }
        };
        _topBarFadeTimer.Start();
    }

    private static Color Lerp(Color from, Color to, double t) => new(
        (byte)(from.A + ((to.A - from.A) * t)),
        (byte)(from.R + ((to.R - from.R) * t)),
        (byte)(from.G + ((to.G - from.G) * t)),
        (byte)(from.B + ((to.B - from.B) * t)));
}
