using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NetWall.IDE.Models;

namespace NetWall.IDE.Controls;

public sealed class SimulationIntro : Control
{
    private const int Cell = 32;
    private const int WalkLeftRow = 7;
    private const int WalkFrames = 8;
    private const int SleepRow = 16;
    private const int SleepFrames = 2;
    private const double HelpySize = 72;
    private const double WallWidth = 70;
    private const double WallHeight = 120;
    private const double DragMilliseconds = 2000;
    private const double MinimumMilliseconds = 2600;
    private const double ExitMilliseconds = 2200;
    private const string Title = "Preparando espacio de simulación";
    private const string ExitTitle = "Saliendo de modo simulación";

    private static readonly FontFamily Font = new("Cascadia Code,Consolas,DejaVu Sans Mono,monospace");

    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Bitmap _sheet = PetWidget.LoadSheet(PetColor.Gray);
    private bool _exiting;

    public SimulationIntro()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(320) }];
        _timer.Tick += (_, _) => InvalidateVisual();
    }

    public async Task PlayExitAsync(PetColor color, Action covered)
    {
        _sheet = PetWidget.LoadSheet(color);
        _exiting = true;
        Opacity = 0;
        IsVisible = true;
        _clock.Restart();
        _timer.Start();
        await Task.Delay(30);
        Opacity = 1;
        await Task.Delay(320);
        covered();

        await Task.Delay((int)ExitMilliseconds);
        Opacity = 0;
        await Task.Delay(340);
        _timer.Stop();
        IsVisible = false;
        _exiting = false;
    }

    public async Task PlayAsync(Task work, PetColor color, Action covered, Action ready)
    {
        _sheet = PetWidget.LoadSheet(color);
        Opacity = 0;
        IsVisible = true;
        _clock.Restart();
        _timer.Start();
        await Task.Delay(30);
        Opacity = 1;
        await Task.Delay(320);
        covered();

        await Task.WhenAll(work, Task.Delay((int)MinimumMilliseconds));
        ready();
        await Task.Delay(120);
        Opacity = 0;
        await Task.Delay(340);
        _timer.Stop();
        IsVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(Brush("BackgroundBrush"), null, bounds);

        var elapsed = _clock.Elapsed.TotalMilliseconds;
        if (_exiting)
        {
            RenderExit(context, bounds, elapsed);
            return;
        }

        var k = Math.Min(1, elapsed / DragMilliseconds);
        var eased = 1 - Math.Pow(1 - k, 3);
        var ground = bounds.Center.Y + 40;
        var helpyX = Lerp(bounds.Width + HelpySize, bounds.Center.X - WallWidth / 2 - 30, eased);
        var wallX = helpyX + 90;
        var wall = new Rect(wallX, ground - WallHeight, WallWidth, WallHeight);
        var walking = k < 1;

        context.DrawLine(new Pen(Brush("BorderBrush"), 1.5), new Point(bounds.Width * 0.2, ground),
            new Point(bounds.Width * 0.8, ground));

        if (walking)
        {
            DrawDust(context, wall, ground, elapsed);
        }

        context.DrawRectangle(Brush("TerminalBackgroundBrush"), null, wall.Inflate(2), 4, 4);
        using (context.PushClip(wall))
        {
            SimulationCanvas.DrawBricks(context, wall, ColorOf("Ansi1Brush"), 0, 0);
        }

        context.DrawRectangle(null, new Pen(Brush("BorderBrush")), wall.Inflate(2), 4, 4);

        var hop = walking ? -Math.Abs(Math.Sin(elapsed / 90)) * 3 : 0;
        var helpy = new Rect(helpyX - HelpySize / 2, ground - HelpySize + 8 + hop, HelpySize, HelpySize);
        var hand = new Point(helpy.Center.X + 14, helpy.Bottom - 26);
        var knot = new Point(wall.Left - 2, wall.Top + 36);
        var sag = walking ? 4 : 14;
        var rope = new StreamGeometry();
        using (var geometry = rope.Open())
        {
            geometry.BeginFigure(hand, false);
            geometry.QuadraticBezierTo(new Point((hand.X + knot.X) / 2, Math.Max(hand.Y, knot.Y) + sag), knot);
            geometry.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Brush("Ansi3Brush"), 2, lineCap: PenLineCap.Round), rope);

        var (row, frame) = walking
            ? (WalkLeftRow, (int)(elapsed / 85) % WalkFrames)
            : (52, (int)(elapsed / 420) % 4);
        context.DrawImage(_sheet, new Rect(frame * Cell, row * Cell, Cell, Cell), helpy);

        DrawProgress(context, bounds, ground, elapsed, Title, MinimumMilliseconds);
    }

    private void RenderExit(DrawingContext context, Rect bounds, double elapsed)
    {
        var ground = bounds.Center.Y + 40;
        context.DrawLine(new Pen(Brush("BorderBrush"), 1.5), new Point(bounds.Width * 0.2, ground),
            new Point(bounds.Width * 0.8, ground));

        var wall = new Rect(bounds.Center.X + 6, ground - WallHeight, WallWidth, WallHeight);
        context.DrawRectangle(Brush("TerminalBackgroundBrush"), null, wall.Inflate(2), 4, 4);
        using (context.PushClip(wall))
        {
            SimulationCanvas.DrawBricks(context, wall, ColorOf("Ansi1Brush"), 0, 0);
        }

        context.DrawRectangle(null, new Pen(Brush("BorderBrush")), wall.Inflate(2), 4, 4);

        var breath = Math.Sin(elapsed / 520) * 1.5;
        var helpy = new Rect(wall.Left - HelpySize + 14, ground - HelpySize + 8 - breath, HelpySize, HelpySize + breath);
        var frame = (int)(elapsed / 600) % SleepFrames;
        context.DrawImage(_sheet, new Rect(frame * Cell, SleepRow * Cell, Cell, Cell), helpy);
        DrawSnores(context, new Point(helpy.Center.X + 6, helpy.Top + 18), elapsed);

        DrawProgress(context, bounds, ground, elapsed, ExitTitle, ExitMilliseconds);
    }

    private void DrawSnores(DrawingContext context, Point origin, double elapsed)
    {
        var accent = ColorOf("AccentBrush");
        for (var i = 0; i < 3; i++)
        {
            var phase = (elapsed / 1500 + i / 3.0) % 1;
            var size = 11 + phase * 9;
            var alpha = phase < 0.15 ? phase / 0.15 : 1 - (phase - 0.15) / 0.85;
            var snore = Format("z", size, new SolidColorBrush(accent, alpha), bold: true);
            var drift = Math.Sin(phase * Math.PI * 2) * 5;
            context.DrawText(snore, new Point(origin.X - 20 + drift - phase * 14, origin.Y - phase * 46));
        }
    }

    private void DrawProgress(DrawingContext context, Rect bounds, double ground, double elapsed, string text,
        double duration)
    {
        var dots = new string('.', (int)(elapsed / 350) % 4);
        var title = Format($"{text}{dots}", 15, Brush("TextPrimaryBrush"), bold: true);
        var titleX = bounds.Center.X - Format(text + "...", 15, Brush("TextPrimaryBrush"), bold: true).Width / 2;
        context.DrawText(title, new Point(titleX, ground + 28));

        var bar = new Rect(bounds.Center.X - 110, ground + 62, 220, 4);
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), null, bar, 2, 2);
        var fill = Math.Min(1, elapsed / duration);
        context.DrawRectangle(Brush("AccentBrush"), null, bar.WithWidth(bar.Width * (1 - Math.Pow(1 - fill, 2))), 2, 2);
    }

    private void DrawDust(DrawingContext context, Rect wall, double ground, double elapsed)
    {
        var dust = ColorOf("TextMutedBrush");
        for (var i = 0; i < 6; i++)
        {
            var phase = (elapsed / 600 + i / 6.0) % 1;
            var center = new Point(wall.Right + 6 + phase * 40, ground - 4 - phase * 14 - i % 2 * 4);
            var radius = 3 + phase * 6;
            context.DrawEllipse(new SolidColorBrush(dust, 0.35 * (1 - phase)), null, center, radius, radius);
        }
    }

    private static double Lerp(double from, double to, double k) => from + (to - from) * k;

    private static FormattedText Format(string text, double size, IBrush brush, bool bold) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Font, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), size, brush);

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;

    private Color ColorOf(string key) => Brush(key) is ISolidColorBrush solid ? solid.Color : Colors.Gray;
}
