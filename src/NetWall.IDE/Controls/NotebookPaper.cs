using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetWall.IDE.Controls;

public sealed class NotebookPaper : Control
{
    private const double LineGap = 26;
    private const double Inset = 16;

    public NotebookPaper()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var line = new Pen(Brush("NotebookLineBrush"), 1);
        for (var y = LineGap * 2; y < Bounds.Height - 8; y += LineGap)
        {
            context.DrawLine(line, new Point(Inset, y), new Point(Bounds.Width - Inset, y));
        }
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;
}
