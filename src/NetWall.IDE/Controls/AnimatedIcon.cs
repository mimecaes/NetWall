using System;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Lucide.Avalonia;

namespace NetWall.IDE.Controls;

public sealed class AnimatedIcon : Path
{
    public static readonly StyledProperty<LucideIconKind?> KindProperty =
        AvaloniaProperty.Register<AnimatedIcon, LucideIconKind?>(nameof(Kind));

    public LucideIconKind? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private Button? _hoverSource;
    private CancellationTokenSource? _drawCts;

    static AnimatedIcon()
    {
        KindProperty.Changed.AddClassHandler<AnimatedIcon>((icon, _) => icon.UpdateGeometry());
    }

    private void UpdateGeometry()
    {
        Data = Kind is { } kind ? Geometry.Parse(kind.GetGeometryData()) : null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _hoverSource = this.FindAncestorOfType<Button>();
        if (_hoverSource != null)
        {
            _hoverSource.PointerEntered += OnHoverSourcePointerEntered;
            _hoverSource.PointerExited += OnHoverSourcePointerExited;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _drawCts?.Cancel();
        if (_hoverSource != null)
        {
            _hoverSource.PointerEntered -= OnHoverSourcePointerEntered;
            _hoverSource.PointerExited -= OnHoverSourcePointerExited;
            _hoverSource = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private async void OnHoverSourcePointerEntered(object? sender, PointerEventArgs e)
    {
        _drawCts?.Cancel();
        var cts = new CancellationTokenSource();
        _drawCts = cts;

        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.00),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.None,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(StrokeDashOffsetProperty, 100d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(StrokeDashOffsetProperty, 0d) } },
            }
        };

        try
        {
            await animation.RunAsync(this, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                StrokeDashOffset = 0;
            }
        }
    }

    private void OnHoverSourcePointerExited(object? sender, PointerEventArgs e)
    {
        _drawCts?.Cancel();
        StrokeDashOffset = 0;
    }
}
