using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetWall.IDE.Controls;

public sealed class HandBubble : Decorator
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<HandBubble, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<HandBubble, IBrush?>(nameof(Stroke));

    private const double TailX = 30;

    static HandBubble()
    {
        AffectsRender<HandBubble>(FillProperty, StrokeProperty);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size).Deflate(2);
        if (rect.Width < 40 || rect.Height < 20)
        {
            return;
        }

        const double radius = 14;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            var start = new Point(rect.Left + radius, rect.Top);
            path.BeginFigure(start, true);
            path.LineTo(new Point(rect.Left + TailX - 7, rect.Top));
            path.LineTo(new Point(rect.Left + TailX - 4, rect.Top - 12));
            path.LineTo(new Point(rect.Left + TailX + 7, rect.Top + 0.5));
            path.QuadraticBezierTo(new Point(rect.Center.X, rect.Top - 1.5), new Point(rect.Right - radius, rect.Top + 0.8));
            path.QuadraticBezierTo(rect.TopRight, new Point(rect.Right + 0.6, rect.Top + radius));
            path.QuadraticBezierTo(new Point(rect.Right + 1.5, rect.Center.Y), new Point(rect.Right, rect.Bottom - radius));
            path.QuadraticBezierTo(rect.BottomRight, new Point(rect.Right - radius, rect.Bottom + 0.6));
            path.QuadraticBezierTo(new Point(rect.Center.X, rect.Bottom + 1.8), new Point(rect.Left + radius, rect.Bottom));
            path.QuadraticBezierTo(rect.BottomLeft, new Point(rect.Left - 0.5, rect.Bottom - radius));
            path.QuadraticBezierTo(new Point(rect.Left - 1.4, rect.Center.Y), new Point(rect.Left, rect.Top + radius));
            path.QuadraticBezierTo(rect.TopLeft, start);
            path.EndFigure(true);
        }

        context.DrawGeometry(Fill, Stroke is null ? null : new Pen(Stroke, 1.8, lineJoin: PenLineJoin.Round), geometry);
    }
}
