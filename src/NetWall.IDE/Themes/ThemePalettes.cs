using System.Collections.Generic;
using Avalonia.Media;
using NetWall.IDE.Models;

namespace NetWall.IDE.Themes;

public static class ThemePalettes
{
    public static IReadOnlyDictionary<string, Color> GetColors(AppTheme theme) => Palettes[theme];

    private static readonly IReadOnlyDictionary<AppTheme, IReadOnlyDictionary<string, Color>> Palettes = new Dictionary<AppTheme, IReadOnlyDictionary<string, Color>>
    {
        [AppTheme.Dark] = Build(
            ("WindowBackgroundBrush", "#1E1E1E"),
            ("EditorBackgroundBrush", "#1E1E1E"),
            ("SidebarBackgroundBrush", "#252526"),
            ("HeaderBackgroundBrush", "#3C3C3C"),
            ("ToolbarBackgroundBrush", "#333333"),
            ("PanelHeaderBackgroundBrush", "#252526"),
            ("TabActiveBackgroundBrush", "#1E1E1E"),
            ("TabInactiveBackgroundBrush", "#2D2D2D"),
            ("IdeBorderBrush", "#3C3C3C"),
            ("TextPrimaryBrush", "#D4D4D4"),
            ("TextMutedBrush", "#9D9D9D"),
            ("TextDisabledBrush", "#6A6A6A"),
            ("AccentBrush", "#007ACC"),
            ("HoverBrush", "#2A2D2E"),
            ("ActiveBrush", "#37373D"),
            ("StatusBarBackgroundBrush", "#007ACC"),
            ("RunGreenBrush", "#3FB950"),
            ("StopRedBrush", "#F14C4C"),
            ("TerminalBackgroundBrush", "#181818")),

        [AppTheme.Pink] = Build(
            ("WindowBackgroundBrush", "#23151C"),
            ("EditorBackgroundBrush", "#23151C"),
            ("SidebarBackgroundBrush", "#2D1B24"),
            ("HeaderBackgroundBrush", "#482E3B"),
            ("ToolbarBackgroundBrush", "#39232E"),
            ("PanelHeaderBackgroundBrush", "#2D1B24"),
            ("TabActiveBackgroundBrush", "#23151C"),
            ("TabInactiveBackgroundBrush", "#35212B"),
            ("IdeBorderBrush", "#4D3340"),
            ("TextPrimaryBrush", "#E4DDE0"),
            ("TextMutedBrush", "#AC9AA3"),
            ("TextDisabledBrush", "#796770"),
            ("AccentBrush", "#DF4994"),
            ("HoverBrush", "#432333"),
            ("ActiveBrush", "#5E2C45"),
            ("StatusBarBackgroundBrush", "#A82466"),
            ("RunGreenBrush", "#3FB950"),
            ("StopRedBrush", "#F14C4C"),
            ("TerminalBackgroundBrush", "#1A0F14")),

        [AppTheme.SkyBlue] = Build(
            ("WindowBackgroundBrush", "#151F23"),
            ("EditorBackgroundBrush", "#151F23"),
            ("SidebarBackgroundBrush", "#1B282D"),
            ("HeaderBackgroundBrush", "#2E4048"),
            ("ToolbarBackgroundBrush", "#233339"),
            ("PanelHeaderBackgroundBrush", "#1B282D"),
            ("TabActiveBackgroundBrush", "#151F23"),
            ("TabInactiveBackgroundBrush", "#213035"),
            ("IdeBorderBrush", "#33454D"),
            ("TextPrimaryBrush", "#DDE2E4"),
            ("TextMutedBrush", "#9AA7AC"),
            ("TextDisabledBrush", "#677479"),
            ("AccentBrush", "#49B4DF"),
            ("HoverBrush", "#233A43"),
            ("ActiveBrush", "#2C505E"),
            ("StatusBarBackgroundBrush", "#2483A8"),
            ("RunGreenBrush", "#3FB950"),
            ("StopRedBrush", "#F14C4C"),
            ("TerminalBackgroundBrush", "#0F171A")),

        [AppTheme.DarkBlue] = Build(
            ("WindowBackgroundBrush", "#151923"),
            ("EditorBackgroundBrush", "#151923"),
            ("SidebarBackgroundBrush", "#1B1F2D"),
            ("HeaderBackgroundBrush", "#2E3448"),
            ("ToolbarBackgroundBrush", "#232839"),
            ("PanelHeaderBackgroundBrush", "#1B1F2D"),
            ("TabActiveBackgroundBrush", "#151923"),
            ("TabInactiveBackgroundBrush", "#212635"),
            ("IdeBorderBrush", "#33394D"),
            ("TextPrimaryBrush", "#DDDFE4"),
            ("TextMutedBrush", "#9A9FAC"),
            ("TextDisabledBrush", "#676C79"),
            ("AccentBrush", "#496EDF"),
            ("HoverBrush", "#232B43"),
            ("ActiveBrush", "#2C385E"),
            ("StatusBarBackgroundBrush", "#2445A8"),
            ("RunGreenBrush", "#3FB950"),
            ("StopRedBrush", "#F14C4C"),
            ("TerminalBackgroundBrush", "#0F121A")),

        [AppTheme.DarkPurple] = Build(
            ("WindowBackgroundBrush", "#1B1523"),
            ("EditorBackgroundBrush", "#1B1523"),
            ("SidebarBackgroundBrush", "#221B2D"),
            ("HeaderBackgroundBrush", "#382E48"),
            ("ToolbarBackgroundBrush", "#2C2339"),
            ("PanelHeaderBackgroundBrush", "#221B2D"),
            ("TabActiveBackgroundBrush", "#1B1523"),
            ("TabInactiveBackgroundBrush", "#2A2135"),
            ("IdeBorderBrush", "#3E334D"),
            ("TextPrimaryBrush", "#E0DDE4"),
            ("TextMutedBrush", "#A29AAC"),
            ("TextDisabledBrush", "#6F6779"),
            ("AccentBrush", "#8749DF"),
            ("HoverBrush", "#302343"),
            ("ActiveBrush", "#412C5E"),
            ("StatusBarBackgroundBrush", "#5B24A8"),
            ("RunGreenBrush", "#3FB950"),
            ("StopRedBrush", "#F14C4C"),
            ("TerminalBackgroundBrush", "#130F1A")),

        [AppTheme.Lilac] = Build(
            ("WindowBackgroundBrush", "#1C1622"),
            ("EditorBackgroundBrush", "#1C1622"),
            ("SidebarBackgroundBrush", "#241C2C"),
            ("HeaderBackgroundBrush", "#3B2F46"),
            ("ToolbarBackgroundBrush", "#2E2438"),
            ("PanelHeaderBackgroundBrush", "#241C2C"),
            ("TabActiveBackgroundBrush", "#1C1622"),
            ("TabInactiveBackgroundBrush", "#2B2334"),
            ("IdeBorderBrush", "#40344B"),
            ("TextPrimaryBrush", "#E0DDE4"),
            ("TextMutedBrush", "#A39AAC"),
            ("TextDisabledBrush", "#706779"),
            ("AccentBrush", "#B290D5"),
            ("HoverBrush", "#362843"),
            ("ActiveBrush", "#47325D"),
            ("StatusBarBackgroundBrush", "#7544A7"),
            ("RunGreenBrush", "#3FB950"),
            ("StopRedBrush", "#F14C4C"),
            ("TerminalBackgroundBrush", "#141019")),

        [AppTheme.Visual] = Build(
            ("WindowBackgroundBrush", "#19181B"),
            ("EditorBackgroundBrush", "#19181B"),
            ("SidebarBackgroundBrush", "#1F1E22"),
            ("HeaderBackgroundBrush", "#2A292E"),
            ("ToolbarBackgroundBrush", "#221F24"),
            ("PanelHeaderBackgroundBrush", "#1F1E22"),
            ("TabActiveBackgroundBrush", "#19181B"),
            ("TabInactiveBackgroundBrush", "#221F24"),
            ("IdeBorderBrush", "#2E2C32"),
            ("TextPrimaryBrush", "#E4E1E6"),
            ("TextMutedBrush", "#9C97A6"),
            ("TextDisabledBrush", "#6B6773"),
            ("AccentBrush", "#9C8FB5"),
            ("HoverBrush", "#29262E"),
            ("ActiveBrush", "#332F3B"),
            ("StatusBarBackgroundBrush", "#4A4358"),
            ("RunGreenBrush", "#3FB950"),
            ("StopRedBrush", "#F14C4C"),
            ("TerminalBackgroundBrush", "#141316")),

        [AppTheme.Light] = Build(
            ("WindowBackgroundBrush", "#FFFFFF"),
            ("EditorBackgroundBrush", "#FFFFFF"),
            ("SidebarBackgroundBrush", "#F3F3F3"),
            ("HeaderBackgroundBrush", "#E8E8E8"),
            ("ToolbarBackgroundBrush", "#F3F3F3"),
            ("PanelHeaderBackgroundBrush", "#F3F3F3"),
            ("TabActiveBackgroundBrush", "#FFFFFF"),
            ("TabInactiveBackgroundBrush", "#ECECEC"),
            ("IdeBorderBrush", "#D4D4D4"),
            ("TextPrimaryBrush", "#1E1E1E"),
            ("TextMutedBrush", "#6E6E6E"),
            ("TextDisabledBrush", "#A0A0A0"),
            ("AccentBrush", "#007ACC"),
            ("HoverBrush", "#E4E6F1"),
            ("ActiveBrush", "#D6DAF0"),
            ("StatusBarBackgroundBrush", "#007ACC"),
            ("RunGreenBrush", "#16825D"),
            ("StopRedBrush", "#CD3131"),
            ("TerminalBackgroundBrush", "#F5F5F5")),
    };

    private static IReadOnlyDictionary<string, Color> Build(params (string Key, string Hex)[] entries)
    {
        var dict = new Dictionary<string, Color>(entries.Length);
        foreach (var (key, hex) in entries)
        {
            dict[key] = Color.Parse(hex);
        }

        return dict;
    }
}
