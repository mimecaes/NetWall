using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetWall.IDE.Controls;

public sealed class NotebookPaper : Control
{
    public static readonly StyledProperty<bool> IsLeftProperty =
        AvaloniaProperty.Register<NotebookPaper, bool>(nameof(IsLeft));

    private const double LineGap = 26;
    private const double MarginX = 22;
    private const double RingGap = 36;
    private const double HoleRadius = 4;
    private const double CurveWidth = 46;

    static NotebookPaper()
    {
        AffectsRender<NotebookPaper>(IsLeftProperty);
    }

    public NotebookPaper()
    {
        IsHitTestVisible = false;
    }

    public bool IsLeft
    {
        get => GetValue(IsLeftProperty);
        set => SetValue(IsLeftProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        DrawGrain(context, width, height);

        var line = new Pen(new SolidColorBrush(ColorOf("NotebookLineBrush"), 0.55), 1);
        for (var y = LineGap * 2; y < height - 8; y += LineGap)
        {
            context.DrawLine(line, new Point(0, y), new Point(width, y));
        }

        var marginX = IsLeft ? MarginX : MarginX + 10;
        var margin = new Pen(new SolidColorBrush(ColorOf("NotebookMarginBrush"), 0.85), 1.4);
        context.DrawLine(margin, new Point(marginX, 0), new Point(marginX, height));

        var inner = IsLeft ? width : 0;
        var direction = IsLeft ? -1 : 1;
        context.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(IsLeft ? 1 : 0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(IsLeft ? 0 : 1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(46, 60, 40, 20), 0),
                new GradientStop(Color.FromArgb(0, 60, 40, 20), 1),
            },
        }, null, new Rect(IsLeft ? width - CurveWidth : 0, 0, CurveWidth, height));

        var count = (int)((height - RingGap) / RingGap) + 1;
        var top = (height - (count - 1) * RingGap) / 2;
        var hole = new SolidColorBrush(ColorOf("NotebookCoverBrush"));
        var rim = new Pen(new SolidColorBrush(Color.FromArgb(60, 60, 40, 20)), 1);
        for (var i = 0; i < count; i++)
        {
            var center = new Point(inner + direction * 9, top + i * RingGap);
            context.DrawEllipse(hole, rim, center, HoleRadius, HoleRadius);
        }
    }

    private static void DrawGrain(DrawingContext context, double width, double height)
    {
        var dark = new SolidColorBrush(Color.FromArgb(14, 90, 70, 40));
        var light = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
        var count = (int)(width * height / 700);
        for (var i = 0; i < count; i++)
        {
            var x = Fraction(Math.Sin(i * 12.9898) * 43758.5453) * width;
            var y = Fraction(Math.Sin(i * 78.233) * 12345.6789) * height;
            context.FillRectangle(i % 3 == 0 ? light : dark, new Rect(x, y, 1.2, 1.2));
        }
    }

    private static double Fraction(double value) => value - Math.Floor(value);

    private Color ColorOf(string key) =>
        this.TryFindResource(key, out var resource) && resource is ISolidColorBrush brush ? brush.Color : Colors.Gray;
}
