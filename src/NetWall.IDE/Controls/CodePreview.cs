using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using NetWall.IDE.Services;
using NetWall.IDE.Themes;

namespace NetWall.IDE.Controls;

public sealed class CodePreview : TextBlock
{
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<CodePreview, string?>(nameof(Code));

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CodeProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var inlines = new InlineCollection();
        var lines = (Code ?? string.Empty).TrimEnd('\n').Split('\n');
        for (var l = 0; l < lines.Length; l++)
        {
            var line = lines[l];
            var position = 0;
            foreach (var span in NetWallSyntaxScanner.ScanLine(line))
            {
                if (span.Start > position)
                {
                    inlines.Add(new Run(line[position..span.Start]));
                }

                var run = new Run(line.Substring(span.Start, span.Length));
                if (ThemeManager.CurrentSyntax[span.Token] is { } brush)
                {
                    run.Foreground = brush;
                }

                inlines.Add(run);
                position = span.Start + span.Length;
            }

            if (position < line.Length)
            {
                inlines.Add(new Run(line[position..]));
            }

            if (l < lines.Length - 1)
            {
                inlines.Add(new LineBreak());
            }
        }

        Inlines = inlines;
    }
}
