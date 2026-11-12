using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetWall.IDE.Controls;

public sealed class WashiTape : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<WashiTape, IBrush?>(nameof(Fill));

    private const double Tooth = 3.5;

    static WashiTape()
    {
        AffectsRender<WashiTape>(FillProperty);
    }

    public WashiTape()
    {
        IsHitTestVisible = false;
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 4 * Tooth || height < 4)
        {
            return;
        }

        var color = Fill is ISolidColorBrush solid ? solid.Color : Colors.Plum;
        var shape = Shape(width, height);
        context.DrawGeometry(new SolidColorBrush(color, 0.62), null, shape);

        using (context.PushGeometryClip(shape))
        {
            var stripe = new Pen(new SolidColorBrush(Colors.White, 0.16), 3);
            for (var x = -height; x < width + height; x += 9)
            {
                context.DrawLine(stripe, new Point(x, height), new Point(x + height, 0));
            }

            var paw = new SolidColorBrush(Shade(color, 0.7), 0.45);
            for (var x = 16.0; x < width - 8; x += 26)
            {
                var center = new Point(x, height / 2 + 1);
                context.DrawEllipse(paw, null, center + new Vector(0, 1.6), 2.6, 2.2);
                foreach (var (dx, dy) in new[] { (-2.8, -1.6), (-1.0, -3.4), (1.0, -3.4), (2.8, -1.6) })
                {
                    context.DrawEllipse(paw, null, center + new Vector(dx, dy), 0.95, 0.95);
                }
            }

            context.DrawLine(new Pen(new SolidColorBrush(Colors.White, 0.28), 1), new Point(0, 1.5),
                new Point(width, 1.5));
        }
    }

    private static StreamGeometry Shape(double width, double height)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        var teeth = Math.Max(2, (int)(height / 4));
        var step = height / teeth;
        path.BeginFigure(new Point(Tooth, 0), true);
        path.LineTo(new Point(width - Tooth, 0));
        for (var i = 0; i < teeth; i++)
        {
            path.LineTo(new Point(width - (i % 2 == 0 ? 0 : Tooth), step * (i + 0.5)));
            path.LineTo(new Point(width - Tooth * (i % 2 == 0 ? 0.6 : 0.2), step * (i + 1)));
        }

        path.LineTo(new Point(Tooth, height));
        for (var i = teeth - 1; i >= 0; i--)
        {
            path.LineTo(new Point(i % 2 == 0 ? 0 : Tooth, step * (i + 0.5)));
            path.LineTo(new Point(Tooth * (i % 2 == 0 ? 0.6 : 0.2), step * i));
        }

        path.EndFigure(true);
        return geometry;
    }

    private static Color Shade(Color color, double factor) =>
        Color.FromRgb((byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));
}
