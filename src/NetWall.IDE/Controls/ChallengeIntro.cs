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

public sealed class ChallengeIntro : Control
{
    private const int Cell = 32;
    private const int WalkRightRow = 6;
    private const int WalkLeftRow = 7;
    private const int WalkFrames = 8;
    private const int SleepRow = 16;
    private const int SleepFrames = 2;
    private const double HelpySize = 72;
    private const double BallRadius = 17;
    private const double BookWidth = 112;
    private const double BookHeight = 144;
    private const double RollMilliseconds = 1700;
    private const double RewindMilliseconds = 1300;
    private const double OpenStart = 1500;
    private const double OpenMilliseconds = 700;
    private const double EnterMilliseconds = 2600;
    private const double ExitMilliseconds = 2400;
    private const string EnterTitle = "Abriendo tu cuaderno de retos";
    private const string ExitTitle = "Guardando tu progreso";

    private static readonly FontFamily Font = new("Cascadia Code,Consolas,DejaVu Sans Mono,monospace");

    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Bitmap _sheet = PetWidget.LoadSheet(PetColor.Gray);
    private bool _exiting;

    public ChallengeIntro()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(320) }];
        _timer.Tick += (_, _) => InvalidateVisual();
    }

    public Task PlayAsync(PetColor color, Action covered) => PlaySceneAsync(color, covered, EnterMilliseconds, false);

    public Task PlayExitAsync(PetColor color, Action covered) =>
        PlaySceneAsync(color, covered, ExitMilliseconds, true);

    private async Task PlaySceneAsync(PetColor color, Action covered, double duration, bool exiting)
    {
        _sheet = PetWidget.LoadSheet(color);
        _exiting = exiting;
        Opacity = 0;
        IsVisible = true;
        _clock.Restart();
        _timer.Start();
        await Task.Delay(30);
        Opacity = 1;
        await Task.Delay(320);
        covered();

        await Task.Delay((int)(duration - 350));
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
        var ground = bounds.Center.Y + 40;
        var start = bounds.Width * 0.22;
        var stop = bounds.Center.X + BookWidth + 70;
        context.DrawLine(new Pen(Brush("BorderBrush"), 1.5), new Point(bounds.Width * 0.18, ground),
            new Point(bounds.Width * 0.82, ground));

        var roll = Math.Min(1, elapsed / (_exiting ? RewindMilliseconds : RollMilliseconds));
        var eased = 1 - Math.Pow(1 - roll, 3);
        var open = _exiting
            ? 1 - Math.Min(1, elapsed / OpenMilliseconds)
            : Math.Clamp((elapsed - OpenStart) / OpenMilliseconds, 0, 1);
        var ballX = _exiting ? Lerp(stop, start, eased) : Lerp(start, stop, eased);

        DrawBook(context, new Point(bounds.Center.X, ground - 112), Smooth(open), elapsed);
        DrawThread(context, start, ballX, ground);
        DrawBall(context, new Point(ballX, ground - BallRadius), (ballX - start) / BallRadius);
        DrawHelpy(context, ballX, ground, elapsed, roll < 1);

        if (_exiting && roll >= 1)
        {
            DrawSnores(context, new Point(ballX + BallRadius + 24, ground - 60), elapsed);
        }

        DrawProgress(context, bounds, ground, elapsed, _exiting ? ExitTitle : EnterTitle,
            _exiting ? ExitMilliseconds : EnterMilliseconds);
    }

    private void DrawBook(DrawingContext context, Point spine, double open, double elapsed)
    {
        var pop = _exiting ? 1 : Math.Clamp(elapsed / 360, 0, 1);
        var scale = 0.6 + 0.4 * (1 - Math.Pow(1 - pop, 3));
        var shift = (1 - open) * BookWidth / 2;
        using var transform = context.PushTransform(Matrix.CreateTranslation(-spine.X, -spine.Y)
                                                    * Matrix.CreateScale(scale, scale)
                                                    * Matrix.CreateTranslation(spine.X - shift, spine.Y));
        using var fade = context.PushOpacity(pop);

        var top = spine.Y - BookHeight / 2;
        var right = new Rect(spine.X, top, BookWidth, BookHeight);
        DrawPage(context, right);

        var flip = Math.Cos(open * Math.PI);
        using (context.PushTransform(Matrix.CreateTranslation(-spine.X, 0) * Matrix.CreateScale(flip, 1)
                                     * Matrix.CreateTranslation(spine.X, 0)))
        {
            if (flip > 0)
            {
                DrawCover(context, right);
            }
            else
            {
                DrawPage(context, right);
            }
        }

        context.DrawLine(new Pen(Brush("BorderBrush"), 2), new Point(spine.X, top), new Point(spine.X, top + BookHeight));
    }

    private void DrawPage(DrawingContext context, Rect page)
    {
        var accent = ColorOf("AccentBrush");
        context.DrawRectangle(Brush("ChallengePaperBrush"), new Pen(new SolidColorBrush(accent, 0.6), 1.5), page, 6, 6);
        var line = new Pen(Brush("BorderBrush"), 1);
        for (var y = page.Y + 18; y < page.Bottom - 8; y += 14)
        {
            context.DrawLine(line, new Point(page.X + 8, y), new Point(page.Right - 8, y));
        }
    }

    private void DrawCover(DrawingContext context, Rect cover)
    {
        var accent = ColorOf("AccentBrush");
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), null, cover, 6, 6);
        context.DrawRectangle(new SolidColorBrush(accent, 0.3), new Pen(new SolidColorBrush(accent), 1.5), cover, 6, 6);
        var paw = new SolidColorBrush(accent, 0.8);
        var center = cover.Center;
        context.DrawEllipse(paw, null, center + new Vector(0, 6), 9, 8);
        foreach (var (x, y) in new[] { (-10.0, -5.0), (-3.5, -11.0), (3.5, -11.0), (10.0, -5.0) })
        {
            context.DrawEllipse(paw, null, center + new Vector(x, y), 3.6, 3.6);
        }
    }

    private void DrawThread(DrawingContext context, double from, double to, double ground)
    {
        if (to - from < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            var y = ground - 3;
            path.BeginFigure(new Point(from, y), false);
            var step = 22.0;
            var up = true;
            for (var x = from; x < to; x += step)
            {
                var end = Math.Min(to, x + step);
                path.QuadraticBezierTo(new Point((x + end) / 2, y + (up ? -4 : 3)), new Point(end, y));
                up = !up;
            }

            path.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Brush("Ansi5Brush"), 2, lineCap: PenLineCap.Round), geometry);
    }

    private void DrawBall(DrawingContext context, Point center, double angle)
    {
        var color = ColorOf("Ansi5Brush");
        context.DrawEllipse(new SolidColorBrush(color, 0.9), null, center, BallRadius, BallRadius);
        using var rotate = context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y)
                                                 * Matrix.CreateRotation(angle)
                                                 * Matrix.CreateTranslation(center.X, center.Y));
        var thread = new Pen(new SolidColorBrush(Colors.Black, 0.25), 1.5);
        foreach (var offset in new[] { -0.45, 0.05, 0.55 })
        {
            var arc = new StreamGeometry();
            using (var path = arc.Open())
            {
                path.BeginFigure(center + new Vector(-BallRadius * 0.85, BallRadius * offset), false);
                path.QuadraticBezierTo(center + new Vector(0, BallRadius * (offset - 0.6)),
                    center + new Vector(BallRadius * 0.85, BallRadius * offset));
                path.EndFigure(false);
            }

            context.DrawGeometry(null, thread, arc);
        }
    }

    private void DrawHelpy(DrawingContext context, double ballX, double ground, double elapsed, bool walking)
    {
        int row;
        int frame;
        double x;
        if (_exiting)
        {
            x = ballX + BallRadius + HelpySize / 2 - 10;
            (row, frame) = walking
                ? (WalkLeftRow, (int)(elapsed / 85) % WalkFrames)
                : (SleepRow, (int)(elapsed / 600) % SleepFrames);
        }
        else
        {
            x = ballX - BallRadius - HelpySize / 2 + 10;
            (row, frame) = walking ? (WalkRightRow, (int)(elapsed / 85) % WalkFrames) : (52, (int)(elapsed / 420) % 4);
        }

        var hop = walking ? -Math.Abs(Math.Sin(elapsed / 90)) * 3 : 0;
        var target = new Rect(x - HelpySize / 2, ground - HelpySize + 8 + hop, HelpySize, HelpySize);
        context.DrawImage(_sheet, new Rect(frame * Cell, row * Cell, Cell, Cell), target);
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
            context.DrawText(snore, new Point(origin.X + drift + phase * 14, origin.Y - phase * 46));
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

    private static double Smooth(double k) => k * k * (3 - 2 * k);

    private static double Lerp(double from, double to, double k) => from + (to - from) * k;

    private static FormattedText Format(string text, double size, IBrush brush, bool bold) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Font, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), size, brush);

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;

    private Color ColorOf(string key) => Brush(key) is ISolidColorBrush solid ? solid.Color : Colors.Gray;
}
