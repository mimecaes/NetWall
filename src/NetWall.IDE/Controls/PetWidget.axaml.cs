using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NetWall.IDE.Models;

namespace NetWall.IDE.Controls;

public partial class PetWidget : UserControl
{
    private enum PetState
    {
        Idle,
        Exercising,
        Sleeping,
    }

    private enum IdleBehavior
    {
        Walking,
        Eating,
        Grooming,
    }

    private const double SleepAfterSeconds = 20;
    private const int ExerciseEditThreshold = 8;
    private const double ExerciseWindowSeconds = 3;
    private const double ExerciseHoldSeconds = 4;

    private const int Cell = 32;
    private const int DisplaySize = 40;
    private const int WalkRightRow = 6;
    private const int WalkLeftRow = 7;
    private const int SleepRow = 16;
    private const int EatRow = 22;
    private const int GroomRow = 39;

    private const int StationIconSize = 34;
    private const int StationIconMargin = 2;
    private const int StationGap = 4;
    private const int FogoFrameCount = 4;
    private const int FogoTicksPerFrame = 8;
    private const int FogoCropX = 579;
    private const int FogoCropY = 579;
    private const int FogoCropWidth = 149;
    private const int FogoCropHeight = 170;

    private static readonly Dictionary<PetColor, Bitmap> SheetCache = new();
    private static IImage[]? _fogoFrames;

    private int _fogoFrame;
    private int _fogoFrameTick;

    private readonly DispatcherTimer _timer;
    private readonly Queue<DateTime> _recentEdits = new();
    private readonly Random _random = new();

    private DateTime _lastActivity = DateTime.UtcNow;
    private DateTime _exerciseUntil = DateTime.MinValue;
    private PetState _state = PetState.Idle;
    private IdleBehavior _idleBehavior = IdleBehavior.Walking;
    private DateTime _idleBehaviorChangeAt = DateTime.UtcNow;

    private double _x = StationIconMargin + StationIconSize + StationGap;
    private int _direction = 1;
    private int _frame;
    private int _frameTick;
    private Bitmap _sheetBitmap;

    public PetWidget()
    {
        InitializeComponent();
        _sheetBitmap = LoadSheet(PetColor.Gray);
        PickNewIdleBehavior();
        FogoIcon.Source = LoadFogoFrames()[0];

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>Call whenever the user edits code; fast bursts trigger the "exercising" state.</summary>
    public void NotifyTyping()
    {
        var now = DateTime.UtcNow;
        _lastActivity = now;
        _recentEdits.Enqueue(now);
    }

    /// <summary>Call on any other activity (caret moves, etc.) that should reset the idle/sleep timer.</summary>
    public void NotifyActivity() => _lastActivity = DateTime.UtcNow;

    /// <summary>Swaps which color sprite sheet is used, taking effect on the next frame.</summary>
    public void SetColor(PetColor color) => _sheetBitmap = LoadSheet(color);

    private static Bitmap LoadSheet(PetColor color)
    {
        if (SheetCache.TryGetValue(color, out var cached))
        {
            return cached;
        }

        var fileName = color switch
        {
            PetColor.Orange => "cat-orange.png",
            PetColor.White => "cat-white.png",
            _ => "cat-gray.png",
        };

        var uri = new Uri($"avares://NetWall.IDE/Assets/pet/{fileName}");
        using var stream = AssetLoader.Open(uri);
        var bitmap = new Bitmap(stream);
        SheetCache[color] = bitmap;
        return bitmap;
    }

    private static IImage[] LoadFogoFrames()
    {
        if (_fogoFrames is not null)
        {
            return _fogoFrames;
        }

        var frames = new IImage[FogoFrameCount];
        for (var i = 0; i < FogoFrameCount; i++)
        {
            var uri = new Uri($"avares://NetWall.IDE/Assets/Icons/fuego_{i + 1}.png");
            using var stream = AssetLoader.Open(uri);
            var bitmap = new Bitmap(stream);
            frames[i] = new CroppedBitmap(bitmap, new PixelRect(FogoCropX, FogoCropY, FogoCropWidth, FogoCropHeight));
        }

        _fogoFrames = frames;
        return frames;
    }

    private CroppedBitmap Frame(int row, int col) =>
        new(_sheetBitmap, new PixelRect(col * Cell, row * Cell, Cell, Cell));

    private void SetFrame(int row, int col) => Sprite.Source = Frame(row, col);

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;

        while (_recentEdits.Count > 0 && (now - _recentEdits.Peek()).TotalSeconds > ExerciseWindowSeconds)
        {
            _recentEdits.Dequeue();
        }

        UpdateState(now);
        AnimateFogo();

        switch (_state)
        {
            case PetState.Sleeping:
                RenderStationary(SleepRow, frameCount: 2, ticksPerFrame: 12);
                break;
            case PetState.Exercising:
                RenderWalking(speed: 4.5, ticksPerFrame: 2);
                break;
            case PetState.Idle:
            default:
                RenderIdle();
                break;
        }
    }

    private void UpdateState(DateTime now)
    {
        if ((now - _lastActivity).TotalSeconds > SleepAfterSeconds)
        {
            _state = PetState.Sleeping;
            return;
        }

        if (_recentEdits.Count >= ExerciseEditThreshold)
        {
            _exerciseUntil = now.AddSeconds(ExerciseHoldSeconds);
        }

        if (now < _exerciseUntil)
        {
            _state = PetState.Exercising;
            return;
        }

        if (_state != PetState.Idle)
        {
            PickNewIdleBehavior();
        }

        _state = PetState.Idle;

        if (now >= _idleBehaviorChangeAt)
        {
            PickNewIdleBehavior();
        }
    }

    private void PickNewIdleBehavior()
    {
        var choices = new[] { IdleBehavior.Walking, IdleBehavior.Walking, IdleBehavior.Eating, IdleBehavior.Grooming };
        _idleBehavior = choices[_random.Next(choices.Length)];
        _idleBehaviorChangeAt = DateTime.UtcNow.AddSeconds(4 + _random.NextDouble() * 5);
        _frame = 0;
        _frameTick = 0;
    }

    private void RenderIdle()
    {
        switch (_idleBehavior)
        {
            case IdleBehavior.Walking:
                RenderWalking(speed: 1.8, ticksPerFrame: 4);
                break;

            case IdleBehavior.Eating:
                RenderStationary(EatRow, frameCount: 8, ticksPerFrame: 4);
                break;

            case IdleBehavior.Grooming:
                RenderStationary(GroomRow, frameCount: 11, ticksPerFrame: 4);
                break;
        }
    }

    private double CanvasWidth()
    {
        var width = PetCanvas.Bounds.Width;
        return width < 40 ? 300 : width;
    }

    private void AnimateFogo()
    {
        var width = CanvasWidth();
        Canvas.SetLeft(FogoIcon, width - StationIconSize - StationIconMargin);

        _fogoFrameTick++;
        if (_fogoFrameTick < FogoTicksPerFrame)
        {
            return;
        }

        _fogoFrameTick = 0;
        _fogoFrame = (_fogoFrame + 1) % FogoFrameCount;
        FogoIcon.Source = LoadFogoFrames()[_fogoFrame];
    }

    private void RenderWalking(double speed, int ticksPerFrame)
    {
        var width = CanvasWidth();

        var minX = StationIconMargin + StationIconSize + StationGap;
        var maxX = width - DisplaySize - StationIconMargin - StationIconSize - StationGap;
        if (maxX < minX)
        {
            maxX = minX;
        }

        _x += _direction * speed;

        if (_x >= maxX)
        {
            _x = maxX;
            _direction = -1;
        }
        else if (_x <= minX)
        {
            _x = minX;
            _direction = 1;
        }

        Canvas.SetLeft(Sprite, _x);
        Canvas.SetTop(Sprite, 0);

        AdvanceFrame(frameCount: 8, ticksPerFrame);
        SetFrame(_direction >= 0 ? WalkRightRow : WalkLeftRow, _frame);
    }

    private void RenderStationary(int row, int frameCount, int ticksPerFrame)
    {
        Canvas.SetTop(Sprite, 0);
        AdvanceFrame(frameCount, ticksPerFrame);
        SetFrame(row, _frame);
    }

    private void AdvanceFrame(int frameCount, int ticksPerFrame)
    {
        _frameTick++;
        if (_frameTick < ticksPerFrame)
        {
            return;
        }

        _frameTick = 0;
        _frame = (_frame + 1) % frameCount;
    }
}
