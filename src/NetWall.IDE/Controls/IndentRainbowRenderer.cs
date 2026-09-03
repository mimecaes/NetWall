using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace NetWall.IDE.Controls;

public sealed class IndentRainbowRenderer : IBackgroundRenderer
{
    private static readonly IBrush[] Colors =
    [
        new SolidColorBrush(Color.FromArgb(15, 255, 255, 64)),  // rgba(255,255,64,0.07)
        new SolidColorBrush(Color.FromArgb(15, 127, 255, 127)), // rgba(127,255,127,0.07)
        new SolidColorBrush(Color.FromArgb(15, 255, 127, 255)), // rgba(255,127,255,0.07)
        new SolidColorBrush(Color.FromArgb(15, 79, 236, 236)),  // rgba(79,236,236,0.07)
    ];

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        var document = textView.Document;
        var indentSize = Math.Max(1, textView.Options.IndentationSize);

        foreach (var visualLine in textView.VisualLines)
        {
            var line = visualLine.FirstDocumentLine;
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

            var top = textView.GetVisualPosition(new TextViewPosition(line.LineNumber, 1), VisualYPosition.LineTop).Y;
            var bottom = textView.GetVisualPosition(new TextViewPosition(line.LineNumber, 1), VisualYPosition.LineBottom).Y;

            for (var i = 0; i < levels; i++)
            {
                var left = textView.GetVisualPosition(new TextViewPosition(line.LineNumber, boundaries[i]), VisualYPosition.LineTop).X;
                var right = textView.GetVisualPosition(new TextViewPosition(line.LineNumber, boundaries[i + 1]), VisualYPosition.LineTop).X;

                drawingContext.FillRectangle(Colors[i % Colors.Length], new Rect(left, top, right - left, bottom - top));
            }
        }
    }
}
