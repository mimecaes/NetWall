using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace NetWall.IDE.Controls;

public sealed class RuleHighlighter : IBackgroundRenderer
{
    private static readonly FontFamily Handwriting = new("avares://NetWall.IDE/Assets/Fonts#Caveat");

    private int _first;
    private int _last;
    private Color _color;
    private string _label = string.Empty;

    public KnownLayer Layer => KnownLayer.Background;

    public double Progress { get; set; }

    public bool IsActive => _first > 0;

    public void Show(int first, int last, Color color, string label)
    {
        _first = first;
        _last = last;
        _color = color;
        _label = label;
        Progress = 0;
    }

    public void Clear() => _first = 0;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_first <= 0)
        {
            return;
        }

        var count = _last - _first + 1;
        var brush = new SolidColorBrush(_color, 0.3);
        Rect? firstRect = null;
        for (var line = _first; line <= _last; line++)
        {
            var visual = textView.GetVisualLine(line);
            if (visual is null)
            {
                continue;
            }

            var k = Math.Clamp(Progress * count - (line - _first), 0, 1);
            if (k <= 0)
            {
                continue;
            }

            var top = visual.VisualTop - textView.VerticalOffset;
            var end = visual.GetVisualPosition(visual.VisualLength, VisualYPosition.LineMiddle).X
                      - textView.HorizontalOffset + 6;
            var rect = new Rect(2, top + 1, Math.Max(0, end - 2) * k, visual.Height - 2);
            drawingContext.DrawRectangle(brush, null, rect, 3, 3);
            firstRect ??= new Rect(2, top + 1, Math.Max(0, end - 2), visual.Height - 2);
        }

        if (firstRect is { } anchor && Progress >= 1)
        {
            var text = new FormattedText(_label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(Handwriting, FontStyle.Normal, FontWeight.Bold), 20, new SolidColorBrush(_color));
            var x = Math.Min(anchor.Right + 10, textView.Bounds.Width - text.Width - 6);
            drawingContext.DrawText(text, new Point(x, anchor.Center.Y - text.Height / 2));
        }
    }
}
