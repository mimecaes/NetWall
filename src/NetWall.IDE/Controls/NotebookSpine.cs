using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetWall.IDE.Controls;

public sealed class NotebookSpine : Control
{
    private const double RingGap = 36;
    private const double RingRadius = 6;

    public NotebookSpine()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var x = Bounds.Width / 2;
        var cover = Brush("NotebookCoverBrush");
        var ring = new Pen(Brush("NotebookRingBrush"), 2);
        var count = (int)((Bounds.Height - RingGap) / RingGap) + 1;
        var top = (Bounds.Height - (count - 1) * RingGap) / 2;
        for (var i = 0; i < count; i++)
        {
            var center = new Point(x, top + i * RingGap);
            context.DrawEllipse(cover, ring, center, RingRadius, RingRadius);
            context.DrawLine(ring, center + new Vector(-RingRadius - 5, 0), center + new Vector(-RingRadius, 0));
            context.DrawLine(ring, center + new Vector(RingRadius, 0), center + new Vector(RingRadius + 5, 0));
        }
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;
}
