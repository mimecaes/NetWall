using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NetWall.IDE.Models;

namespace NetWall.IDE.Controls;

public enum HelpyMood
{
    Idle,
    Talking,
    Happy,
}

/// <summary>
/// Helpy, the code assistant mascot: the same pixel cat as the terminal pet, animated with
/// a sprite row per mood.
/// </summary>
public sealed class HelpySprite : Image
{
    private const int Cell = 32;

    public static readonly StyledProperty<HelpyMood> MoodProperty =
        AvaloniaProperty.Register<HelpySprite, HelpyMood>(nameof(Mood));

    private readonly DispatcherTimer _timer = new();
    private Bitmap _sheet = PetWidget.LoadSheet(PetColor.Gray);
    private int _frame;

    public HelpySprite()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Stretch = Stretch.Uniform;
        _timer.Tick += (_, _) => NextFrame();
        UpdateTiming();
    }

    public HelpyMood Mood
    {
        get => GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    public void SetColor(PetColor color) => _sheet = PetWidget.LoadSheet(color);

    // Row, frame count and frame duration of each mood in the sprite sheet.
    private (int Row, int Frames, int Milliseconds) Animation => Mood switch
    {
        HelpyMood.Talking => (32, 8, 90),
        HelpyMood.Happy => (41, 2, 160),
        _ => (52, 4, 420),
    };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        NextFrame();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MoodProperty)
        {
            _frame = 0;
            UpdateTiming();
            NextFrame();
        }
    }

    private void UpdateTiming() => _timer.Interval = TimeSpan.FromMilliseconds(Animation.Milliseconds);

    private void NextFrame()
    {
        var (row, frames, _) = Animation;
        Source = new CroppedBitmap(_sheet, new PixelRect(_frame % frames * Cell, row * Cell, Cell, Cell));
        _frame = (_frame + 1) % frames;
    }
}
