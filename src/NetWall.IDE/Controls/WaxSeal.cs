using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace NetWall.IDE.Controls;

public sealed class WaxSeal : Control
{
    public static readonly StyledProperty<int> LevelProperty = AvaloniaProperty.Register<WaxSeal, int>(nameof(Level));

    private const double DropMilliseconds = 560;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();

    static WaxSeal()
    {
        AffectsRender<WaxSeal>(LevelProperty);
    }

    public WaxSeal()
    {
        _timer.Tick += (_, _) =>
        {
            InvalidateVisual();
            if (_clock.Elapsed.TotalMilliseconds > DropMilliseconds)
            {
                _timer.Stop();
                _clock.Reset();
            }
        };
    }

    public int Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public void Drop()
    {
        _clock.Restart();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        var center = new Rect(Bounds.Size).Center;
        var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 6;
        if (radius <= 4)
        {
            return;
        }

        if (Level <= 0)
        {
            DrawSlot(context, center, radius);
            return;
        }

        var color = ColorOf(Level >= 2 ? "SealGoldBrush" : "SealPinkBrush");
        var dark = Shade(color, 0.72);
        var k = _clock.IsRunning ? Math.Clamp(_clock.Elapsed.TotalMilliseconds / DropMilliseconds, 0, 1) : 1;
        var eased = 1 - Math.Pow(1 - k, 3);
        var scale = 1 + 0.9 * (1 - eased);
        using var opacity = context.PushOpacity(Math.Min(1, k * 2.5));
        if (k < 1)
        {
            DrawDrops(context, center, radius, color, eased);
        }

        using var transform = context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y)
                                                    * Matrix.CreateScale(scale, scale)
                                                    * Matrix.CreateRotation(-0.2)
                                                    * Matrix.CreateTranslation(center.X, center.Y));
        context.DrawGeometry(new SolidColorBrush(color), null, Blob(center, radius));
        context.DrawEllipse(null, new Pen(new SolidColorBrush(dark), 2), center, radius * 0.7, radius * 0.7);
        DrawFish(context, center, radius, dark, color);
        var shine = new StreamGeometry();
        using (var path = shine.Open())
        {
            path.BeginFigure(center + new Vector(-radius * 0.62, -radius * 0.2), false);
            path.QuadraticBezierTo(center + new Vector(-radius * 0.55, -radius * 0.6),
                center + new Vector(-radius * 0.18, -radius * 0.7));
            path.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.White, 0.35), 2.2, lineCap: PenLineCap.Round),
            shine);
    }

    private void DrawSlot(DrawingContext context, Point center, double radius)
    {
        var muted = ColorOf("NotebookMutedBrush");
        context.DrawEllipse(null, new Pen(new SolidColorBrush(muted, 0.7), 1.5, new DashStyle([3, 3], 0)), center,
            radius * 0.82, radius * 0.82);
        var paw = new SolidColorBrush(muted, 0.4);
        var size = radius / 22;
        context.DrawEllipse(paw, null, center + new Vector(0, 4 * size), 6 * size, 5 * size);
        foreach (var (x, y) in new[] { (-7.0, -4.0), (-2.5, -8.5), (2.5, -8.5), (7.0, -4.0) })
        {
            context.DrawEllipse(paw, null, center + new Vector(x * size, y * size), 2.6 * size, 2.6 * size);
        }
    }

    private static void DrawDrops(DrawingContext context, Point center, double radius, Color color, double eased)
    {
        var brush = new SolidColorBrush(color, 1 - eased);
        for (var i = 0; i < 7; i++)
        {
            var angle = i * Math.PI * 2 / 7 + 0.4;
            var distance = radius * (0.9 + 0.7 * eased);
            var point = center + new Vector(Math.Cos(angle), Math.Sin(angle)) * distance;
            context.DrawEllipse(brush, null, point, 3.2 * (1 - eased * 0.6), 3.2 * (1 - eased * 0.6));
        }
    }

    private static void DrawFish(DrawingContext context, Point center, double radius, Color dark, Color light)
    {
        var body = new SolidColorBrush(dark);
        context.DrawEllipse(body, null, center + new Vector(-radius * 0.08, 0), radius * 0.34, radius * 0.2);
        var tail = new StreamGeometry();
        using (var path = tail.Open())
        {
            path.BeginFigure(center + new Vector(radius * 0.2, 0), true);
            path.LineTo(center + new Vector(radius * 0.44, -radius * 0.2));
            path.LineTo(center + new Vector(radius * 0.44, radius * 0.2));
            path.EndFigure(true);
        }

        context.DrawGeometry(body, null, tail);
        context.DrawEllipse(new SolidColorBrush(light), null, center + new Vector(-radius * 0.26, -radius * 0.04),
            radius * 0.05, radius * 0.05);
    }

    private static StreamGeometry Blob(Point center, double radius)
    {
        const int count = 12;
        var points = new Point[count];
        for (var i = 0; i < count; i++)
        {
            var angle = i * Math.PI * 2 / count;
            var wobble = radius * (1 + 0.07 * Math.Sin(i * 2.3) + 0.04 * Math.Cos(i * 4.1));
            points[i] = center + new Vector(Math.Cos(angle), Math.Sin(angle)) * wobble;
        }

        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        path.BeginFigure((points[^1] + points[0]) / 2, true);
        for (var i = 0; i < count; i++)
        {
            path.QuadraticBezierTo(points[i], (points[i] + points[(i + 1) % count]) / 2);
        }

        path.EndFigure(true);
        return geometry;
    }

    private static Color Shade(Color color, double factor) =>
        Color.FromRgb((byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));

    private Color ColorOf(string key) =>
        this.TryFindResource(key, out var resource) && resource is ISolidColorBrush brush ? brush.Color : Colors.Gray;
}
