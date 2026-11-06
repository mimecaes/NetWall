using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NetWall.IDE.Controls;

public sealed class RuledStack : Panel
{
    public const double Gap = 26;

    public static readonly StyledProperty<bool> FromBottomProperty =
        AvaloniaProperty.Register<RuledStack, bool>(nameof(FromBottom));

    private Visual? _page;
    private double _top;
    private double _lead;

    public bool FromBottom
    {
        get => GetValue(FromBottomProperty);
        set => SetValue(FromBottomProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
        _page = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var content = 0.0;
        foreach (var child in Children.Where(child => child.IsVisible))
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            content += Slot(child);
        }

        if (FromBottom)
        {
            var bottom = _top + Bounds.Height;
            _lead = 0;
            return new Size(width, bottom - Math.Floor((bottom - content) / Gap) * Gap);
        }

        _lead = Math.Ceiling(_top / Gap - 0.01) * Gap - _top;
        return new Size(width, _lead + content);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = _lead;
        foreach (var child in Children.Where(child => child.IsVisible))
        {
            var height = child.DesiredSize.Height;
            var slot = Slot(child);
            var inner = height - child.Margin.Top - child.Margin.Bottom;
            var offset = IsFlow(child) ? 0 : (slot - child.Margin.Top - child.Margin.Bottom - inner) / 2;
            child.Arrange(new Rect(0, y + offset, finalSize.Width, height));
            y += slot;
        }

        return finalSize;
    }

    private static bool IsFlow(Control child) => child is ItemsControl or RuledStack;

    private static double Slot(Control child)
    {
        var margins = child.Margin.Top + child.Margin.Bottom;
        var lines = (child.DesiredSize.Height - margins) / Gap;
        if (IsFlow(child) || Math.Abs(lines - Math.Round(lines)) < 0.05)
        {
            return Math.Max(1, Math.Ceiling(lines - 0.05)) * Gap + margins;
        }

        var odd = Math.Max(1, Math.Round(lines));
        return (odd % 2 == 0 ? odd + 1 : odd) * Gap + margins;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!IsEffectivelyVisible)
        {
            return;
        }

        _page ??= this.GetVisualAncestors().OfType<Panel>()
            .FirstOrDefault(panel => panel.Children.OfType<NotebookPaper>().Any());
        if (_page is null || this.TranslatePoint(default, _page) is not { } point)
        {
            return;
        }

        var scroll = this.FindAncestorOfType<ScrollViewer>() is { } viewer && _page.IsVisualAncestorOf(viewer)
            ? viewer.Offset.Y
            : 0;
        var top = point.Y + scroll;
        if (Math.Abs(top - _top) > 0.5)
        {
            _top = top;
            InvalidateMeasure();
        }
    }
}
