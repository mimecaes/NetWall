using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Controls;

public sealed class ChallengeCheckStage : Control
{
    private const double DesignWidth = 840;
    private const double DesignHeight = 400;
    private const double CardWidth = 78;
    private const double CardHeight = 100;
    private const double CardGap = 10;
    private const double CardsTop = 270;
    private const double Intro = 300;
    private const double Outro = 900;

    private static readonly FontFamily Mono = new("Cascadia Code,Consolas,DejaVu Sans Mono,monospace");
    private static readonly Rect Wall = new(DesignWidth / 2 - 26, 64, 52, 160);
    private static readonly Point Origin = new(150, 144);
    private static readonly Point Destination = new(DesignWidth - 150, 144);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();
    private IReadOnlyList<ChallengeOutcome> _outcomes = [];
    private TaskCompletionSource? _done;
    private double _skip;
    private double _step = 900;

    public ChallengeCheckStage()
    {
        _timer.Tick += OnTick;
    }

    public Task PlayAsync(IReadOnlyList<ChallengeOutcome> outcomes)
    {
        _outcomes = outcomes;
        _step = outcomes.Count switch
        {
            <= 4 => 900,
            <= 6 => 750,
            _ => 600,
        };
        _skip = 0;
        _done = new TaskCompletionSource();
        _clock.Restart();
        _timer.Start();
        InvalidateVisual();
        return _done.Task;
    }

    private double Total => Intro + _outcomes.Count * _step + Outro;

    private double Elapsed => _clock.Elapsed.TotalMilliseconds + _skip;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _skip = Math.Max(_skip, Total - Outro - _clock.Elapsed.TotalMilliseconds);
        e.Handled = true;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        InvalidateVisual();
        if (Elapsed >= Total)
        {
            _timer.Stop();
            _done?.TrySetResult();
        }
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_outcomes.Count == 0)
        {
            return;
        }

        var scale = Math.Min(Bounds.Width / DesignWidth, Bounds.Height / DesignHeight);
        var offset = new Vector((Bounds.Width - DesignWidth * scale) / 2, (Bounds.Height - DesignHeight * scale) / 2);
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale)
                                                    * Matrix.CreateTranslation(offset.X, offset.Y));

        var elapsed = Elapsed;
        var active = (int)Math.Floor((elapsed - Intro) / _step);
        var local = elapsed - Intro - active * _step;
        var current = active >= 0 && active < _outcomes.Count ? _outcomes[active] : null;

        DrawTitle(context, elapsed, active);
        DrawEndpoints(context);
        DrawWall(context, current, local);
        if (current is not null)
        {
            DrawAction(context, current, local);
        }

        for (var i = 0; i < _outcomes.Count; i++)
        {
            DrawCard(context, i, elapsed - Intro - i * _step);
        }
    }

    private void DrawTitle(DrawingContext context, double elapsed, int active)
    {
        string text;
        IBrush brush;
        if (active >= _outcomes.Count)
        {
            var failed = _outcomes.Count(outcome => !outcome.Passed);
            text = _outcomes.Count == 1 ? failed == 0 ? "¡Correcto!" : "Incorrecto"
                : failed == 0 ? "¡Todas las pruebas pasan!"
                : failed == 1 ? "Falló 1 prueba"
                : $"Fallaron {failed} pruebas";
            brush = Brush(failed == 0 ? "RunGreenBrush" : "StopRedBrush");
        }
        else
        {
            text = $"{(_outcomes.Count == 1 ? "Comprobando" : "Comprobando tus pruebas")}{new string('.', (int)(elapsed / 350) % 4)}";
            brush = Brush("TextPrimaryBrush");
        }

        var title = Format(text, 16, brush, true, Mono);
        context.DrawText(title, new Point(DesignWidth / 2 - title.Width / 2, 14));
        var hint = Format("clic para saltar", 10.5, Brush("TextDisabledBrush"), false, Mono);
        if (active < _outcomes.Count)
        {
            context.DrawText(hint, new Point(DesignWidth - hint.Width - 16, 18));
        }
    }

    private void DrawEndpoints(DrawingContext context)
    {
        var muted = Brush("TextMutedBrush");
        var wire = new Pen(new SolidColorBrush(ColorOf("TextMutedBrush"), 0.35), 1.5);
        context.DrawLine(wire, Origin, new Point(Wall.Left, Origin.Y));
        context.DrawLine(wire, new Point(Wall.Right, Destination.Y), Destination);
        foreach (var (point, label) in new[] { (Origin, "origen"), (Destination, "destino") })
        {
            var box = new Rect(point.X - 30, point.Y - 22, 60, 44);
            context.DrawRectangle(Brush("SurfaceBackgroundBrush"), new Pen(Brush("BorderBrush"), 1.5), box, 10, 10);
            var text = Format(label, 11, muted, false, Mono);
            context.DrawText(text, new Point(point.X - text.Width / 2, box.Bottom + 6));
            context.DrawRectangle(null, new Pen(muted, 1.4), new Rect(point.X - 12, point.Y - 10, 24, 16), 2, 2);
            context.DrawLine(new Pen(muted, 1.6), new Point(point.X - 7, point.Y + 11), new Point(point.X + 7, point.Y + 11));
        }
    }

    private void DrawWall(DrawingContext context, ChallengeOutcome? current, double local)
    {
        var action = Math.Clamp((local - _step * 0.25) / (_step * 0.6), 0, 1);
        var blocked = current is { Allowed: false } && action > 0.5;
        var shake = blocked ? Math.Sin(action * 60) * 3 * (1 - action) : 0;
        var wall = Wall.Translate(new Vector(shake, 0));
        var door = current is { Allowed: true } && action is > 0.35 and < 0.95
            ? Math.Sin((action - 0.35) / 0.6 * Math.PI)
            : 0;

        context.DrawRectangle(Brush("TerminalBackgroundBrush"), null, wall.Inflate(2), 4, 4);
        using (context.PushClip(wall))
        {
            SimulationCanvas.DrawBricks(context, wall, ColorOf("Ansi1Brush"), door, Origin.Y);
            if (blocked)
            {
                context.DrawRectangle(new SolidColorBrush(ColorOf("StopRedBrush"), 0.35 * (1 - action)), null, wall);
            }

            if (current is { Allowed: null } && action is > 0 and < 1)
            {
                var y = wall.Y + action * wall.Height;
                var accent = ColorOf("AccentBrush");
                context.DrawRectangle(new SolidColorBrush(accent, 0.2), null, new Rect(wall.X, y - 10, wall.Width, 20));
                context.DrawLine(new Pen(new SolidColorBrush(accent), 2), new Point(wall.X, y), new Point(wall.Right, y));
            }
        }

        context.DrawRectangle(null, new Pen(Brush(current is null ? "BorderBrush" : "AccentBrush"), current is null ? 1 : 2),
            wall.Inflate(2), 4, 4);
        var label = Format("NetWall", 12, Brush("TextPrimaryBrush"), true, Mono);
        context.DrawText(label, new Point(Wall.Center.X - label.Width / 2, Wall.Bottom + 8));
    }

    private void DrawAction(DrawingContext context, ChallengeOutcome outcome, double local)
    {
        var action = Math.Clamp((local - _step * 0.25) / (_step * 0.6), 0, 1);
        if (outcome.Allowed is not { } allowed || action <= 0 || action >= 1)
        {
            return;
        }

        Point point;
        string brush;
        if (action < 0.5)
        {
            point = Lerp(Origin, new Point(Wall.Left - 10, Origin.Y), Ease(action * 2));
            brush = "AccentBrush";
        }
        else if (allowed)
        {
            point = Lerp(new Point(Wall.Left - 10, Origin.Y), Destination, Ease((action - 0.5) * 2));
            brush = "RunGreenBrush";
        }
        else
        {
            var back = Math.Sin((action - 0.5) * 2 * Math.PI / 2);
            point = new Point(Wall.Left - 10 - back * 70, Origin.Y);
            brush = "StopRedBrush";
        }

        var color = ColorOf(brush);
        var body = new Rect(point.X - 13, point.Y - 9, 26, 18);
        context.DrawRectangle(new SolidColorBrush(color, 0.25), null, body.Inflate(3), 5, 5);
        context.DrawRectangle(new SolidColorBrush(color), null, body, 3, 3);
        var flap = new StreamGeometry();
        using (var path = flap.Open())
        {
            path.BeginFigure(body.TopLeft + new Vector(1, 1), false);
            path.LineTo(new Point(point.X, point.Y + 1));
            path.LineTo(body.TopRight + new Vector(-1, 1));
            path.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Brush("TerminalBackgroundBrush"), 1.5), flap);
    }

    private void DrawCard(DrawingContext context, int index, double local)
    {
        var outcome = _outcomes[index];
        var width = _outcomes.Count * CardWidth + (_outcomes.Count - 1) * CardGap;
        var lift = local >= 0 && local < _step ? -8 : 0;
        var card = new Rect(DesignWidth / 2 - width / 2 + index * (CardWidth + CardGap), CardsTop + lift, CardWidth,
            CardHeight);
        var flip = Math.Clamp(local / (_step * 0.25), 0, 1);
        var faceUp = flip >= 0.5;
        var squeeze = Math.Abs(Math.Cos(flip * Math.PI));
        var resolved = local >= _step * 0.85;
        var accent = ColorOf("AccentBrush");

        using var transform = context.PushTransform(Matrix.CreateTranslation(-card.Center.X, 0)
                                                    * Matrix.CreateScale(Math.Max(0.02, squeeze), 1)
                                                    * Matrix.CreateTranslation(card.Center.X, 0));
        if (!faceUp)
        {
            context.DrawRectangle(new SolidColorBrush(accent, 0.3), new Pen(new SolidColorBrush(accent, 0.7), 1.5), card, 10, 10);
            context.DrawRectangle(null, new Pen(new SolidColorBrush(accent, 0.4), 1, new DashStyle([3, 3], 0)),
                card.Deflate(6), 6, 6);
            var mark = Format("?", 22, new SolidColorBrush(accent), true, Mono);
            context.DrawText(mark, new Point(card.Center.X - mark.Width / 2, card.Center.Y - mark.Height / 2));
            return;
        }

        var border = resolved ? outcome.Passed ? "RunGreenBrush" : "StopRedBrush" : "AccentBrush";
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), new Pen(Brush(border), resolved ? 2 : 1.5), card, 10, 10);
        if (resolved)
        {
            context.DrawRectangle(new SolidColorBrush(ColorOf(border), 0.12), null, card, 10, 10);
        }

        var title = new FormattedText(outcome.Test.Visible ? outcome.Test.Label : "Prueba oculta",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 10.5, Brush("TextPrimaryBrush"))
        {
            MaxTextWidth = CardWidth - 12,
            MaxLineCount = 4,
            TextAlignment = TextAlignment.Center,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        context.DrawText(title, new Point(card.X + 6, card.Y + 8));
        if (resolved)
        {
            var glyph = Format(outcome.Passed ? "✓" : "✕", 18, Brush(border), true, Mono);
            context.DrawText(glyph, new Point(card.Center.X - glyph.Width / 2, card.Bottom - glyph.Height - 6));
        }
    }

    private static double Ease(double k) => k < 0.5 ? 2 * k * k : 1 - Math.Pow(-2 * k + 2, 2) / 2;

    private static Point Lerp(Point from, Point to, double k) => from + (to - from) * k;

    private static FormattedText Format(string text, double size, IBrush brush, bool bold, FontFamily font) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(font, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), size, brush);

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;

    private Color ColorOf(string key) => Brush(key) is ISolidColorBrush solid ? solid.Color : Colors.Gray;
}
