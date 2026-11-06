using System;
using System.Linq;
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
    Watching,
}

public sealed class HelpySprite : Image
{
    private const int Cell = 32;

    public static readonly StyledProperty<HelpyMood> MoodProperty =
        AvaloniaProperty.Register<HelpySprite, HelpyMood>(nameof(Mood));

    public static readonly StyledProperty<bool> FidgetsProperty =
        AvaloniaProperty.Register<HelpySprite, bool>(nameof(Fidgets));

    private static readonly (int Row, int Column)[][] Quirks =
    [
        [(32, 0), (52, 0)],
        [(32, 0), (52, 0)],
        [(32, 0), (32, 1), (32, 2), (32, 3), (32, 4), (32, 5), (32, 6), (32, 7)],
        [(52, 0), (2, 3), (2, 3), (2, 3), (2, 3), (52, 0)],
        [.. Enumerable.Range(0, 11).Select(column => (39, column))],
    ];

    private readonly DispatcherTimer _timer = new();
    private Bitmap _sheet = PetWidget.LoadSheet(PetColor.Gray);
    private (int Row, int Column)[] _frames = [];
    private int _frame;
    private int _loops;
    private int _patience;
    private bool _acting;
    private bool _paused;
    private (Bitmap Sheet, int Row, int Column)? _shownFrame;

    public HelpySprite()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Stretch = Stretch.Uniform;
        _timer.Tick += (_, _) =>
        {
            if (IsEffectivelyVisible)
            {
                NextFrame();
            }
        };
        Start();
    }

    public HelpyMood Mood
    {
        get => GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    public bool Fidgets
    {
        get => GetValue(FidgetsProperty);
        set => SetValue(FidgetsProperty, value);
    }

    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            UpdateTimer();
        }
    }

    public void SetColor(PetColor color) => _sheet = PetWidget.LoadSheet(color);

    private void UpdateTimer() => _timer.IsEnabled = !_paused && VisualRoot is not null;

    private ((int Row, int Column)[] Frames, int Milliseconds) Animation => Mood switch
    {
        HelpyMood.Talking => (Row(32, 8), 90),
        HelpyMood.Happy => (Row(41, 2), 160),
        HelpyMood.Watching => ([(39, 2)], 400),
        _ => (Row(52, 4), 420),
    };

    private static (int Row, int Column)[] Row(int row, int count) =>
        [.. Enumerable.Range(0, count).Select(column => (row, column))];

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        NextFrame();
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
        if (change.Property == MoodProperty)
        {
            Start();
            NextFrame();
        }
    }

    private void Start()
    {
        var (frames, milliseconds) = Animation;
        _frames = frames;
        _frame = 0;
        _loops = 0;
        _patience = Random.Shared.Next(3, 7);
        _acting = false;
        _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
    }

    private void Act()
    {
        _frames = Mood == HelpyMood.Watching
            ? [(39, 3), (39, 2)]
            : Quirks[Random.Shared.Next(Quirks.Length)];
        _frame = 0;
        _acting = true;
        _timer.Interval = TimeSpan.FromMilliseconds(Mood == HelpyMood.Watching ? 140 : 170);
    }

    private void NextFrame()
    {
        var (row, column) = _frames[_frame];
        if (_shownFrame != (_sheet, row, column))
        {
            _shownFrame = (_sheet, row, column);
            Source = new CroppedBitmap(_sheet, new PixelRect(column * Cell, row * Cell, Cell, Cell));
        }

        if (++_frame < _frames.Length)
        {
            return;
        }

        _frame = 0;
        if (_acting)
        {
            Start();
        }
        else if (Fidgets && Mood is HelpyMood.Idle or HelpyMood.Watching && ++_loops >= _patience)
        {
            Act();
        }
    }
}
