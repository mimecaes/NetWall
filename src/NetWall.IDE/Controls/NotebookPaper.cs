using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using NetWall.IDE.Themes;

namespace NetWall.IDE.Controls;

public sealed class NotebookPaper : Control
{
    private const double LineGap = 28;
    private const double MarginX = 36;
    private const double RingX = 17;
    private const double RingGap = 56;
    private const double RingRadius = 5;

    private readonly DispatcherTimer _fade = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private DateTime _fadeUntil;

    public NotebookPaper()
    {
        IsHitTestVisible = false;
        _fade.Tick += (_, _) =>
        {
            InvalidateVisual();
            if (DateTime.UtcNow > _fadeUntil)
            {
                _fade.Stop();
            }
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ThemeManager.ThemeApplied += OnThemeApplied;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ThemeManager.ThemeApplied -= OnThemeApplied;
        _fade.Stop();
    }

    public override void Render(DrawingContext context)
    {
        var line = new Pen(Brush("ChallengeLineBrush"), 1);
        for (var y = LineGap; y < Bounds.Height; y += LineGap)
        {
            context.DrawLine(line, new Point(0, y), new Point(Bounds.Width, y));
        }

        var accent = Brush("AccentBrush") is ISolidColorBrush solid ? solid.Color : Colors.Gray;
        context.DrawLine(new Pen(new SolidColorBrush(accent, 0.35), 1.5), new Point(MarginX, 0),
            new Point(MarginX, Bounds.Height));

        var ring = new Pen(Brush("BorderBrush"), 1.5);
        for (var y = RingGap / 2; y < Bounds.Height; y += RingGap)
        {
            context.DrawEllipse(Brush("BackgroundBrush"), ring, new Point(RingX, y), RingRadius, RingRadius);
        }
    }

    private void OnThemeApplied(object? sender, EventArgs e)
    {
        _fadeUntil = DateTime.UtcNow.AddMilliseconds(450);
        _fade.Start();
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;
}
