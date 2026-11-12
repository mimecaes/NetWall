using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetWall.IDE.Controls;

public sealed class HandUnderline : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<HandUnderline, IBrush?>(nameof(Stroke));

    static HandUnderline()
    {
        AffectsRender<HandUnderline>(StrokeProperty);
    }

    public HandUnderline()
    {
        IsHitTestVisible = false;
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var middle = Bounds.Height / 2;
        if (width < 8)
        {
            return;
        }

        var color = Stroke is ISolidColorBrush solid ? solid.Color : Colors.Plum;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(2, middle + 1), false);
            var steps = Math.Max(3, (int)(width / 18));
            for (var i = 1; i <= steps; i++)
            {
                var x = 2 + (width - 6) * i / steps;
                var previous = 2 + (width - 6) * (i - 1) / steps;
                var lift = (i % 2 == 0 ? 1 : -1) * (2.2 + 0.6 * Math.Sin(i * 1.7));
                path.QuadraticBezierTo(new Point((previous + x) / 2, middle + lift), new Point(x, middle + 0.4 * Math.Sin(i)));
            }

            path.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(new SolidColorBrush(color, 0.85), 4, lineCap: PenLineCap.Round), geometry);
    }
}
