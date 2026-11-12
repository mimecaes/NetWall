using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NetWall.IDE.Controls;

public sealed class PageStack : Control
{
    public static readonly StyledProperty<int> CountProperty = AvaloniaProperty.Register<PageStack, int>(nameof(Count));

    public static readonly StyledProperty<bool> IsLeftProperty =
        AvaloniaProperty.Register<PageStack, bool>(nameof(IsLeft));

    private const double Step = 2;

    static PageStack()
    {
        AffectsRender<PageStack>(CountProperty, IsLeftProperty);
    }

    public PageStack()
    {
        IsHitTestVisible = false;
    }

    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public bool IsLeft
    {
        get => GetValue(IsLeftProperty);
        set => SetValue(IsLeftProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var paper = this.TryFindResource("NotebookPageBrush", out var resource) && resource is ISolidColorBrush brush
            ? brush.Color
            : Colors.Beige;
        var edge = new Pen(new SolidColorBrush(Color.FromArgb(70, 90, 70, 40)), 0.8);
        for (var i = Count; i >= 1; i--)
        {
            var shade = 1 - i * 0.045;
            var color = Color.FromRgb((byte)(paper.R * shade), (byte)(paper.G * shade), (byte)(paper.B * shade));
            var offset = i * Step;
            var rect = IsLeft
                ? new Rect(Bounds.Width - offset - 4, offset * 0.6, 4 + offset, Bounds.Height - offset * 1.2)
                : new Rect(0, offset * 0.6, 4 + offset, Bounds.Height - offset * 1.2);
            context.DrawRectangle(new SolidColorBrush(color), edge, rect, 6, 6);
        }
    }
}
