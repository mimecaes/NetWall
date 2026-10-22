using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NetWall.IDE.Models;
using NetWall.IDE.Themes;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Controls;

public sealed class ChallengeBoard : Control
{
    private const int Columns = 5;
    private const int Rows = 4;
    private const double Tile = 112;
    private const double Gap = 14;
    private const double Padding = 26;
    private const double MaxScale = 1.35;
    private const double Grow = 0.06;
    private const double HelpySize = 50;
    private const int SpriteCell = 32;
    private const int Deck = -1;

    private static readonly FontFamily Mono = new("Cascadia Code,Consolas,DejaVu Sans Mono,monospace");

    private static readonly (int Column, int Row)[] Track =
    [
        (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (4, 1), (4, 2),
        (4, 3), (3, 3), (2, 3), (1, 3), (0, 3), (0, 2), (0, 1),
    ];

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<int, double> _grow = [];
    private IReadOnlyList<ChallengeLevel> _levels = [];
    private Bitmap _sheet = PetWidget.LoadSheet(PetColor.Gray);
    private double _scale = 1;
    private Vector _offset;
    private int? _hover;
    private long _lastFrame;

    public ChallengeBoard()
    {
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        _timer.Tick += OnTick;
    }

    public event Action<ChallengeLevel>? LevelClicked;

    public IReadOnlyList<ChallengeLevel> Levels
    {
        get => _levels;
        set
        {
            foreach (var level in _levels)
            {
                level.PropertyChanged -= OnLevelChanged;
            }

            _levels = value;
            foreach (var level in _levels)
            {
                level.PropertyChanged += OnLevelChanged;
            }

            InvalidateVisual();
        }
    }

    public void SetHelpyColor(PetColor color)
    {
        _sheet = PetWidget.LoadSheet(color);
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ThemeManager.ThemeApplied += OnThemeApplied;
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ThemeManager.ThemeApplied -= OnThemeApplied;
        _timer.Stop();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var hover = TargetAt(e.GetPosition(this));
        if (hover != _hover)
        {
            _hover = hover;
            Cursor = new Cursor(hover is not null ? StandardCursorType.Hand : StandardCursorType.Arrow);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left || TargetAt(e.GetPosition(this)) is not { } target)
        {
            return;
        }

        if ((target == Deck ? NextIndex() : target) is { } index)
        {
            LevelClicked?.Invoke(_levels[index]);
        }
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_levels.Count == 0)
        {
            return;
        }

        var size = BoardSize();
        _scale = Math.Min(MaxScale, Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height));
        _offset = new Vector((Bounds.Width - size.Width * _scale) / 2, (Bounds.Height - size.Height * _scale) / 2);
        using var transform = context.PushTransform(Matrix.CreateScale(_scale, _scale)
                                                    * Matrix.CreateTranslation(_offset.X, _offset.Y));

        DrawBoard(context, size);
        DrawTrail(context);
        DrawStart(context);
        for (var i = 0; i < _levels.Count && i + 1 < Track.Length - 1; i++)
        {
            DrawSquare(context, i);
        }

        DrawFinish(context);
        DrawCenter(context);
        DrawHelpy(context);
    }

    private static Size BoardSize() =>
        new(2 * Padding + Columns * Tile + (Columns - 1) * Gap, 2 * Padding + Rows * Tile + (Rows - 1) * Gap);

    private static Rect Square(int track)
    {
        var (column, row) = Track[track];
        return new Rect(Padding + column * (Tile + Gap), Padding + row * (Tile + Gap), Tile, Tile);
    }

    private static Rect Center() =>
        new(Padding + Tile + Gap * 2, Padding + Tile + Gap * 2, (Columns - 2) * Tile, (Rows - 2) * Tile);

    private static Rect DeckCard()
    {
        var center = Center();
        return new Rect(center.Right - 128, center.Y + 34, 114, 158);
    }

    private int? NextIndex()
    {
        var index = _levels.ToList().FindIndex(level => level is { IsUnlocked: true, Fish: 0 });
        return index >= 0 ? index : null;
    }

    private int? TargetAt(Point position)
    {
        var point = new Point((position.X - _offset.X) / _scale, (position.Y - _offset.Y) / _scale);
        if (NextIndex() is not null && DeckCard().Contains(point))
        {
            return Deck;
        }

        for (var i = 0; i < _levels.Count; i++)
        {
            if (_levels[i].IsUnlocked && Square(i + 1).Contains(point))
            {
                return i;
            }
        }

        return null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var changed = false;
        foreach (var key in Enumerable.Range(Deck, _levels.Count + 1))
        {
            var current = _grow.GetValueOrDefault(key);
            var target = key == _hover ? 1 : 0;
            if (Math.Abs(current - target) < 0.01)
            {
                changed |= current != target;
                _grow[key] = target;
                continue;
            }

            _grow[key] = current + (target - current) * 0.25;
            changed = true;
        }

        if (changed || _clock.ElapsedMilliseconds - _lastFrame > 120)
        {
            _lastFrame = _clock.ElapsedMilliseconds;
            InvalidateVisual();
        }
    }

    private void OnLevelChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private void OnThemeApplied(object? sender, EventArgs e) => InvalidateVisual();

    private void DrawBoard(DrawingContext context, Size size)
    {
        context.DrawRectangle(Brush("PanelBackgroundBrush"), new Pen(Brush("BorderBrush"), 2),
            new Rect(size).Deflate(1), 26, 26);
        var inner = Center().Inflate(Gap / 2);
        context.DrawRectangle(Brush("ChallengePaperBrush"),
            new Pen(Brush("ChallengeLineBrush"), 1.5, new DashStyle([6, 5], 0)), inner, 18, 18);
    }

    private void DrawTrail(DrawingContext context)
    {
        for (var i = 0; i < Track.Length - 1; i++)
        {
            var completed = i == 0 || i - 1 < _levels.Count && _levels[i - 1].Fish > 0;
            var pen = completed
                ? new Pen(Brush("AccentBrush"), 3, lineCap: PenLineCap.Round)
                : new Pen(Brush("TextDisabledBrush"), 2.5, new DashStyle([0.5, 2.4], 0), PenLineCap.Round);
            context.DrawLine(pen, Square(i).Center, Square(i + 1).Center);
        }
    }

    private void DrawStart(DrawingContext context)
    {
        var square = Square(0);
        var accent = ColorOf("AccentBrush");
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), new Pen(new SolidColorBrush(accent, 0.8), 1.5), square,
            16, 16);
        context.DrawRectangle(new SolidColorBrush(accent, 0.1), null, square, 16, 16);
        DrawYarn(context, square.Center + new Vector(0, -12), 15, ColorOf("Ansi5Brush"), 0.9);
        DrawCentered(context, "Salida", 13, Brush("TextPrimaryBrush"), square.Center.X, square.Y + 68, Tile, true);
        DrawCentered(context, "→", 14, Brush("AccentBrush"), square.Center.X, square.Y + 86, Tile, true);
    }

    private void DrawFinish(DrawingContext context)
    {
        var square = Square(Track.Length - 1);
        var gold = ColorOf("Ansi3Brush");
        var graduated = _levels.Count > 0 && _levels.All(level => level.Fish > 0);
        using var opacity = context.PushOpacity(graduated ? 1 : 0.5);
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), new Pen(new SolidColorBrush(gold, 0.8), 1.5), square, 16,
            16);
        context.DrawRectangle(new SolidColorBrush(gold, 0.1), null, square, 16, 16);
        var cup = new StreamGeometry();
        using (var path = cup.Open())
        {
            var c = square.Center + new Vector(0, -14);
            path.BeginFigure(c + new Vector(-16, -16), true);
            path.LineTo(c + new Vector(16, -16));
            path.QuadraticBezierTo(c + new Vector(16, 8), c + new Vector(3, 10));
            path.LineTo(c + new Vector(3, 16));
            path.LineTo(c + new Vector(10, 16));
            path.LineTo(c + new Vector(10, 20));
            path.LineTo(c + new Vector(-10, 20));
            path.LineTo(c + new Vector(-10, 16));
            path.LineTo(c + new Vector(-3, 16));
            path.LineTo(c + new Vector(-3, 10));
            path.QuadraticBezierTo(c + new Vector(-16, 8), c + new Vector(-16, -16));
            path.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(gold, 0.85), new Pen(new SolidColorBrush(gold), 1.2), cup);
        DrawCentered(context, "Meta", 13, Brush("TextPrimaryBrush"), square.Center.X, square.Y + 68, Tile, true);
        DrawCentered(context, graduated ? "¡graduado!" : "termina todo", 10.5, Brush("TextMutedBrush"),
            square.Center.X, square.Y + 88, Tile, false);
    }

    private void DrawSquare(DrawingContext context, int index)
    {
        var level = _levels[index];
        var square = Square(index + 1);
        var tint = ColorOf(level.Module.Brush);
        var isNext = index == NextIndex();
        var grow = 1 + Grow * _grow.GetValueOrDefault(index);
        using var scale = context.PushTransform(Matrix.CreateTranslation(-square.Center.X, -square.Center.Y)
                                                * Matrix.CreateScale(grow, grow)
                                                * Matrix.CreateTranslation(square.Center.X, square.Center.Y));
        context.DrawRectangle(Brush("PanelBackgroundBrush"), null, square, 16, 16);
        using var opacity = context.PushOpacity(level.IsUnlocked ? 1 : 0.4);

        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), null, square, 16, 16);
        context.DrawRectangle(new SolidColorBrush(tint, level.Fish > 0 ? 0.2 : 0.08), null, square, 16, 16);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(tint, isNext ? 1 : 0.7), isNext ? 2.5 : 1.5), square,
            16, 16);
        if (isNext)
        {
            context.DrawRectangle(null, new Pen(Brush("AccentBrush"), 1.5, new DashStyle([4, 3], 0)),
                square.Inflate(5), 20, 20);
        }

        if (level.Module.Challenges.Count == 1)
        {
            DrawCrown(context, square.Center + new Vector(0, 2), 2.4, 0.3);
        }
        else
        {
            DrawDecoration(context, index, square.Center + new Vector(0, 4), tint);
        }

        DrawCentered(context, level.Challenge.Id, 12.5, new SolidColorBrush(tint), square.Center.X, square.Y + 12,
            Tile - 12, true);
        var title = new FormattedText(level.Challenge.Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 11, Brush("TextPrimaryBrush"))
        {
            MaxTextWidth = Tile - 14,
            MaxLineCount = 2,
            TextAlignment = TextAlignment.Center,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        context.DrawText(title, new Point(square.X + 7, square.Y + 56 - title.Height / 2));

        for (var f = 0; f < 3; f++)
        {
            DrawFish(context, new Point(square.Center.X + (f - 1) * 20, square.Bottom - 16),
                f < level.Fish ? Brush("Ansi4Brush") : Brush("TextDisabledBrush"));
        }
    }

    private static void DrawDecoration(DrawingContext context, int index, Point center, Color tint)
    {
        var faint = new SolidColorBrush(tint, 0.1);
        switch (index % 4)
        {
            case 0:
                using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(3.2, 3.2)
                                             * Matrix.CreateTranslation(center.X, center.Y)))
                {
                    DrawFish(context, center, faint);
                }

                break;
            case 1:
                DrawYarn(context, center, 22, tint, 0.1);
                break;
            case 2:
                DrawPaw(context, center, 2.4, -12, faint);
                break;
            default:
                DrawTuna(context, center, tint);
                break;
        }
    }

    private void DrawCenter(DrawingContext context)
    {
        var center = Center();
        DrawText(context, "Aprende NetWall", 20, Brush("TextPrimaryBrush"), new Point(center.X + 14, center.Y + 10),
            true, FontFamily.Default);
        DrawText(context, "Un curso de firewalls paso a paso", 11.5, Brush("TextMutedBrush"),
            new Point(center.X + 14, center.Y + 40), false, FontFamily.Default);

        var y = center.Y + 72;
        foreach (var module in _levels.GroupBy(level => level.Module))
        {
            var tint = ColorOf(module.Key.Brush);
            context.DrawEllipse(new SolidColorBrush(tint), null, new Point(center.X + 20, y + 8), 5, 5);
            DrawText(context, module.Key.Name, 12, Brush("TextPrimaryBrush"), new Point(center.X + 32, y), false,
                FontFamily.Default);
            DrawText(context, $"{module.Count(level => level.Fish > 0)}/{module.Count()}", 11.5,
                Brush("TextMutedBrush"), new Point(center.X + 160, y + 1), false, Mono);
            y += 24;
        }

        var done = _levels.Count(level => level.Fish > 0);
        var fish = _levels.Sum(level => level.Fish);
        DrawFish(context, new Point(center.X + 22, center.Bottom - 18), Brush("Ansi4Brush"));
        DrawText(context, $"{fish} pescaditos · {done} de {_levels.Count} retos", 11.5, Brush("TextMutedBrush"),
            new Point(center.X + 36, center.Bottom - 26), false, Mono);

        DrawDeck(context);
    }

    private void DrawDeck(DrawingContext context)
    {
        var card = DeckCard();
        var accent = ColorOf("AccentBrush");
        foreach (var angle in new[] { -9.0, -4.0 })
        {
            using var rotate = context.PushTransform(Rotation(card.Center, angle));
            context.DrawRectangle(new SolidColorBrush(accent, 0.25), new Pen(new SolidColorBrush(accent, 0.6), 1.5),
                card, 12, 12);
            context.DrawRectangle(null, new Pen(new SolidColorBrush(accent, 0.35), 1, new DashStyle([3, 3], 0)),
                card.Deflate(8), 8, 8);
        }

        if (NextIndex() is not { } next)
        {
            return;
        }

        var level = _levels[next];
        var tint = ColorOf(level.Module.Brush);
        var grow = 1 + Grow * _grow.GetValueOrDefault(Deck);
        using var face = context.PushTransform(Rotation(card.Center, 3)
                                               * Matrix.CreateTranslation(-card.Center.X, -card.Center.Y)
                                               * Matrix.CreateScale(grow, grow)
                                               * Matrix.CreateTranslation(card.Center.X, card.Center.Y));
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), new Pen(new SolidColorBrush(tint), 2), card, 12, 12);
        using (context.PushClip(new RoundedRect(card, 12)))
        {
            context.DrawRectangle(new SolidColorBrush(tint, 0.22), null, new Rect(card.X, card.Y, card.Width, 34));
        }

        DrawCentered(context, $"Reto {level.Number}", 12.5, new SolidColorBrush(tint), card.Center.X, card.Y + 9,
            card.Width - 12, true);
        DrawCentered(context, level.Challenge.Id, 11, Brush("TextMutedBrush"), card.Center.X, card.Y + 46,
            card.Width - 12, false);
        var title = new FormattedText(level.Challenge.Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 12.5, Brush("TextPrimaryBrush"))
        {
            MaxTextWidth = card.Width - 18,
            MaxLineCount = 3,
            TextAlignment = TextAlignment.Center,
        };
        context.DrawText(title, new Point(card.X + 9, card.Y + 68));
        DrawCentered(context, "clic para jugar", 10.5, Brush("AccentBrush"), card.Center.X, card.Bottom - 26,
            card.Width - 12, false);
    }

    private void DrawHelpy(DrawingContext context)
    {
        var square = NextIndex() is { } next ? Square(next + 1)
            : _levels.Count > 0 && _levels.All(level => level.Fish > 0) ? Square(Track.Length - 1)
            : Square(0);
        var hop = Math.Abs(Math.Sin(_clock.ElapsedMilliseconds / 260.0)) * 3;
        var foot = new Point(square.Right - 18, square.Y + 20);
        context.DrawEllipse(new SolidColorBrush(Colors.Black, 0.2), null, foot, 14, 4);
        var frame = (int)(_clock.ElapsedMilliseconds / 420 % 4);
        var target = new Rect(foot.X - HelpySize / 2, foot.Y - HelpySize + 8 - hop, HelpySize, HelpySize);
        context.DrawImage(_sheet, new Rect(frame * SpriteCell, 52 * SpriteCell, SpriteCell, SpriteCell), target);
    }

    private void DrawCrown(DrawingContext context, Point center, double size, double alpha)
    {
        using var scale = context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y)
                                                * Matrix.CreateScale(size, size)
                                                * Matrix.CreateTranslation(center.X, center.Y));
        var crown = new StreamGeometry();
        using (var path = crown.Open())
        {
            path.BeginFigure(center + new Vector(-12, 6), true);
            path.LineTo(center + new Vector(-12, -6));
            path.LineTo(center + new Vector(-6, 0));
            path.LineTo(center + new Vector(0, -9));
            path.LineTo(center + new Vector(6, 0));
            path.LineTo(center + new Vector(12, -6));
            path.LineTo(center + new Vector(12, 6));
            path.EndFigure(true);
        }

        var gold = ColorOf("Ansi3Brush");
        context.DrawGeometry(new SolidColorBrush(gold, alpha), null, crown);
    }

    private static void DrawFish(DrawingContext context, Point center, IBrush brush)
    {
        var tail = new StreamGeometry();
        using (var path = tail.Open())
        {
            path.BeginFigure(center + new Vector(4, 0), true);
            path.LineTo(center + new Vector(9, -4));
            path.LineTo(center + new Vector(9, 4));
            path.EndFigure(true);
        }

        context.DrawEllipse(brush, null, center + new Vector(-1, 0), 6, 3.6);
        context.DrawGeometry(brush, null, tail);
    }

    private static void DrawYarn(DrawingContext context, Point center, double radius, Color color, double alpha)
    {
        context.DrawEllipse(new SolidColorBrush(color, alpha), null, center, radius, radius);
        var thread = new Pen(new SolidColorBrush(color, Math.Min(1, alpha * 1.8)), 1.5);
        foreach (var offset in new[] { -0.45, 0.05, 0.55 })
        {
            var arc = new StreamGeometry();
            using (var path = arc.Open())
            {
                path.BeginFigure(center + new Vector(-radius * 0.85, radius * offset), false);
                path.QuadraticBezierTo(center + new Vector(0, radius * (offset - 0.6)),
                    center + new Vector(radius * 0.85, radius * offset));
                path.EndFigure(false);
            }

            context.DrawGeometry(null, thread, arc);
        }
    }

    private static void DrawPaw(DrawingContext context, Point center, double size, double angle, IBrush brush)
    {
        using var rotate = context.PushTransform(Rotation(center, angle));
        context.DrawEllipse(brush, null, center + new Vector(0, 3 * size), 4.5 * size, 4 * size);
        foreach (var (x, y) in new[] { (-5.0, -3.0), (-1.8, -6.0), (1.8, -6.0), (5.0, -3.0) })
        {
            context.DrawEllipse(brush, null, center + new Vector(x * size, y * size), 2 * size, 2 * size);
        }
    }

    private static void DrawTuna(DrawingContext context, Point center, Color tint)
    {
        var pen = new Pen(new SolidColorBrush(tint, 0.18), 2);
        context.DrawRectangle(null, pen, new Rect(center.X - 24, center.Y - 8, 48, 22), 3, 3);
        context.DrawEllipse(null, pen, new Point(center.X, center.Y - 8), 24, 7);
    }

    private static Matrix Rotation(Point center, double degrees) =>
        Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateRotation(degrees * Math.PI / 180)
        * Matrix.CreateTranslation(center.X, center.Y);

    private static void DrawCentered(DrawingContext context, string text, double size, IBrush brush, double centerX,
        double top, double maxWidth, bool bold)
    {
        var formatted = Format(text, size, brush, bold, Mono);
        while (formatted.Width > maxWidth && text.Length > 2)
        {
            text = text[..^2] + "…";
            formatted = Format(text, size, brush, bold, Mono);
        }

        context.DrawText(formatted, new Point(centerX - formatted.Width / 2, top));
    }

    private static void DrawText(DrawingContext context, string text, double size, IBrush brush, Point origin,
        bool bold, FontFamily font) =>
        context.DrawText(Format(text, size, brush, bold, font), origin);

    private static FormattedText Format(string text, double size, IBrush brush, bool bold, FontFamily font) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(font, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), size, brush);

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;

    private Color ColorOf(string key) => Brush(key) is ISolidColorBrush solid ? solid.Color : Colors.Gray;
}
