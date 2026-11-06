using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace NetWall.IDE.Controls;

public sealed class NetworkBackground : Control
{
    private const int NodeCount = 34;
    private const double LinkDistance = 150;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Point[] _positions = new Point[NodeCount];
    private readonly Vector[] _velocities = new Vector[NodeCount];

    public NetworkBackground()
    {
        IsHitTestVisible = false;
        var random = new Random(7);
        for (var i = 0; i < NodeCount; i++)
        {
            _positions[i] = new Point(random.NextDouble(), random.NextDouble());
            var angle = random.NextDouble() * Math.PI * 2;
            var speed = 0.0004 + random.NextDouble() * 0.0006;
            _velocities[i] = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
        }

        _timer.Tick += (_, _) => Step();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
        {
            UpdateTimer();
        }
    }

    private void UpdateTimer() => _timer.IsEnabled = IsVisible && VisualRoot is not null;

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var accent = this.TryFindResource("AccentBrush", out var resource) && resource is ISolidColorBrush brush
            ? brush.Color
            : Colors.MediumPurple;

        var points = new Point[NodeCount];
        for (var i = 0; i < NodeCount; i++)
        {
            points[i] = new Point(_positions[i].X * width, _positions[i].Y * height);
        }

        for (var i = 0; i < NodeCount; i++)
        {
            for (var j = i + 1; j < NodeCount; j++)
            {
                var distance = Point.Distance(points[i], points[j]);
                if (distance < LinkDistance)
                {
                    var alpha = (byte)(70 * (1 - distance / LinkDistance));
                    context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(alpha, accent.R, accent.G, accent.B)), 1),
                        points[i], points[j]);
                }
            }
        }

        var node = new SolidColorBrush(Color.FromArgb(120, accent.R, accent.G, accent.B));
        foreach (var point in points)
        {
            context.DrawEllipse(node, null, point, 2.2, 2.2);
        }
    }

    private void Step()
    {
        if (!IsEffectivelyVisible)
        {
            return;
        }

        for (var i = 0; i < NodeCount; i++)
        {
            var next = _positions[i] + _velocities[i];
            if (next.X is < 0 or > 1)
            {
                _velocities[i] = new Vector(-_velocities[i].X, _velocities[i].Y);
            }

            if (next.Y is < 0 or > 1)
            {
                _velocities[i] = new Vector(_velocities[i].X, -_velocities[i].Y);
            }

            _positions[i] += _velocities[i];
        }

        InvalidateVisual();
    }
}
