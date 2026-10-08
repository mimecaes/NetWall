using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using NetWall.IDE.Services;
using NetWall.IDE.Themes;

namespace NetWall.IDE.Controls;

/// <summary>
/// Colors NetWall tokens in the editor using the syntax palette of the active theme.
/// </summary>
public sealed class NetWallSyntaxColorizer : DocumentColorizingTransformer
{
    public bool IsEnabled { get; set; }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (!IsEnabled)
        {
            return;
        }

        var palette = ThemeManager.CurrentSyntax;
        var text = CurrentContext.Document.GetText(line);

        foreach (var span in NetWallSyntaxScanner.ScanLine(text))
        {
            if (palette[span.Token] is not { } brush)
            {
                continue;
            }

            var start = line.Offset + span.Start;
            ChangeLinePart(start, start + span.Length,
                element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }
}
