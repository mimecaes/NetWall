using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetWall.IDE.Controls;

public enum DoodleKind
{
    Paw,
    Star,
    Yarn,
    Fish,
    FishBone,
    Box,
}

public sealed class Doodle : Control
{
    public static readonly StyledProperty<DoodleKind> KindProperty =
        AvaloniaProperty.Register<Doodle, DoodleKind>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Doodle, IBrush?>(nameof(Stroke));

    static Doodle()
    {
        AffectsRender<Doodle>(KindProperty, StrokeProperty);
    }

    public Doodle()
    {
        IsHitTestVisible = false;
    }

    public DoodleKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var brush = Stroke ?? Brushes.Gray;
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var center = new Rect(Bounds.Size).Center;
        var unit = size / 24;
        var pen = new Pen(brush, Math.Max(1.2, 1.6 * unit), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        switch (Kind)
        {
            case DoodleKind.Paw:
                context.DrawEllipse(brush, null, center + new Vector(0, 3.5 * unit), 5.5 * unit, 4.6 * unit);
                foreach (var (x, y) in new[] { (-6.5, -3.0), (-2.4, -7.2), (2.4, -7.2), (6.5, -3.0) })
                {
                    context.DrawEllipse(brush, null, center + new Vector(x * unit, y * unit), 2.3 * unit, 2.6 * unit);
                }

                break;
            case DoodleKind.Star:
                var star = new StreamGeometry();
                using (var path = star.Open())
                {
                    for (var i = 0; i <= 10; i++)
                    {
                        var angle = -Math.PI / 2 + i * Math.PI / 5;
                        var radius = (i % 2 == 0 ? 10.5 : 4.4) * unit * (1 + 0.05 * Math.Sin(i * 2.7));
                        var point = center + new Vector(Math.Cos(angle), Math.Sin(angle)) * radius;
                        if (i == 0)
                        {
                            path.BeginFigure(point, true);
                        }
                        else
                        {
                            path.LineTo(point);
                        }
                    }

                    path.EndFigure(true);
                }

                context.DrawGeometry(null, pen, star);
                break;
            case DoodleKind.Yarn:
                context.DrawEllipse(null, pen, center, 8.5 * unit, 8.5 * unit);
                foreach (var offset in new[] { -0.45, 0.05, 0.55 })
                {
                    Arc(context, pen, center + new Vector(-7.2 * unit, 8.5 * unit * offset),
                        center + new Vector(0, 8.5 * unit * (offset - 0.6)),
                        center + new Vector(7.2 * unit, 8.5 * unit * offset));
                }

                Arc(context, pen, center + new Vector(6 * unit, 6 * unit), center + new Vector(11 * unit, 8 * unit),
                    center + new Vector(10 * unit, 11 * unit));
                break;
            case DoodleKind.Fish:
                context.DrawEllipse(null, pen, center + new Vector(-2 * unit, 0), 7.5 * unit, 4.6 * unit);
                Polyline(context, pen, true, center + new Vector(5 * unit, 0), center + new Vector(10.5 * unit, -4.5 * unit),
                    center + new Vector(10.5 * unit, 4.5 * unit));
                context.DrawEllipse(brush, null, center + new Vector(-6 * unit, -1 * unit), 0.9 * unit, 0.9 * unit);
                break;
            case DoodleKind.FishBone:
                Polyline(context, pen, false, center + new Vector(-6 * unit, 0), center + new Vector(7 * unit, 0));
                for (var i = 0; i < 4; i++)
                {
                    var x = (-3.5 + i * 3) * unit;
                    Polyline(context, pen, false, center + new Vector(x - unit, -4 * unit), center + new Vector(x + unit, 0),
                        center + new Vector(x - unit, 4 * unit));
                }

                Polyline(context, pen, true, center + new Vector(-6 * unit, 0), center + new Vector(-11 * unit, -4 * unit),
                    center + new Vector(-11 * unit, 4 * unit));
                Polyline(context, pen, true, center + new Vector(7 * unit, 0), center + new Vector(11 * unit, -3.5 * unit),
                    center + new Vector(11 * unit, 3.5 * unit));
                break;
            default:
                var half = size / 2 - 1.5;
                var a = center + new Vector(-half, -half + 0.6);
                var b = center + new Vector(half - 0.4, -half);
                var c = center + new Vector(half, half - 0.5);
                var d = center + new Vector(-half + 0.5, half);
                Arc(context, pen, a, (a + b) / 2 + new Vector(0, -0.8), b);
                Arc(context, pen, b, (b + c) / 2 + new Vector(0.7, 0), c);
                Arc(context, pen, c, (c + d) / 2 + new Vector(0, 0.8), d);
                Arc(context, pen, d, (d + a) / 2 + new Vector(-0.6, 0), a + new Vector(0.6, -0.4));
                break;
        }
    }

    private static void Arc(DrawingContext context, IPen pen, Point from, Point control, Point to)
    {
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(from, false);
            path.QuadraticBezierTo(control, to);
            path.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static void Polyline(DrawingContext context, IPen pen, bool closed, params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(points[0], false);
            foreach (var point in points[1..])
            {
                path.LineTo(point);
            }

            path.EndFigure(closed);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}
