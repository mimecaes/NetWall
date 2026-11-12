using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace NetWall.IDE.Controls;

public sealed class IndentGuideRenderer : IBackgroundRenderer
{
    private const byte BaseAlpha = 100;
    private const double LineWidth = 1.0;

    public double Opacity { get; set; } = 1.0;
    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid || Opacity <= 0)
        {
            return;
        }

        var pen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb((byte)(BaseAlpha * Opacity), 128, 128, 128)), LineWidth);
        var document = textView.Document;
        var indentSize = Math.Max(1, textView.Options.IndentationSize);

        foreach (var visualLine in textView.VisualLines)
        {
            var line = visualLine.FirstDocumentLine;
            if (line.Length == 0 || document.GetCharAt(line.Offset) is not (' ' or '\t'))
            {
                continue;
            }

            var text = document.GetText(line);
            var boundaries = new List<int> { 1 };
            var visualWidth = 0;
            var charIndex = 0;

            while (charIndex < text.Length)
            {
                var ch = text[charIndex];
                if (ch == ' ')
                {
                    visualWidth += 1;
                }
                else if (ch == '\t')
                {
                    visualWidth = (visualWidth / indentSize + 1) * indentSize;
                }
                else
                {
                    break;
                }

                charIndex++;
                if (visualWidth % indentSize == 0)
                {
                    boundaries.Add(charIndex + 1);
                }
            }

            var levels = boundaries.Count - 1;
            if (levels == 0)
            {
                continue;
            }

            var scrollOffset = textView.ScrollOffset;
            var top = textView.GetVisualPosition(new TextViewPosition(line.LineNumber, 1), VisualYPosition.LineTop).Y - scrollOffset.Y;
            var bottom = textView.GetVisualPosition(new TextViewPosition(line.LineNumber, 1), VisualYPosition.LineBottom).Y - scrollOffset.Y;

            for (var i = 1; i < boundaries.Count; i++)
            {
                var xPos = textView.GetVisualPosition(new TextViewPosition(line.LineNumber, boundaries[i]), VisualYPosition.LineTop).X - scrollOffset.X - 5;

                drawingContext.DrawLine(
                    pen,
                    new Point(xPos, top),
                    new Point(xPos, bottom)
                );
            }
        }
    }
}
