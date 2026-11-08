using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace NetWall.IDE.Controls;

public sealed class ClassIntro : Control
{
    private const int Columns = 6;
    private const int Rows = 5;
    private const int Seats = Columns * Rows;
    private const double SeatGap = 54;
    private const double ScreenWidth = 28;
    private const double ScreenHeight = 18;
    private const double EnterMilliseconds = 2200;
    private const double ExitMilliseconds = 1700;
    private const double SeatsShare = 0.72;
    private const string EnterTitle = "Encendiendo el laboratorio";
    private const string ExitTitle = "Apagando el laboratorio";

    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private bool _exiting;

    public ClassIntro()
    {
        IsVisible = false;
        Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(320) }];
        _timer.Tick += (_, _) => InvalidateVisual();
    }

    public Task PlayAsync(Action covered) => PlaySceneAsync(covered, EnterMilliseconds, false);

    public Task PlayExitAsync(Action covered) => PlaySceneAsync(covered, ExitMilliseconds, true);

    private async Task PlaySceneAsync(Action covered, double duration, bool exiting)
    {
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

        var duration = _exiting ? ExitMilliseconds : EnterMilliseconds;
        var progress = Math.Clamp(_clock.Elapsed.TotalMilliseconds / (duration * SeatsShare), 0, 1);
        var lit = _exiting ? 1 - progress : progress;
        var accent = Color("AccentBrush");
        var origin = new Point(bounds.Center.X - (Columns - 1) * SeatGap / 2, bounds.Center.Y - 76);
        var hub = new Point(bounds.Center.X, origin.Y - 56);

        var rail = new Pen(Brush("BorderBrush"), 1.2);
        context.DrawLine(rail, hub, new Point(hub.X, origin.Y - 26));
        context.DrawLine(rail, new Point(origin.X, origin.Y - 26), new Point(origin.X + (Columns - 1) * SeatGap, origin.Y - 26));
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), new Pen(new SolidColorBrush(accent), 1.5),
            new Rect(hub.X - 56, hub.Y - 18, 112, 36), 8, 8);
        DrawText(context, "Firewall", hub, 13, Brush("TextPrimaryBrush"));

        for (var i = 0; i < Seats; i++)
        {
            var seat = origin + new Vector(i % Columns * SeatGap, i / Columns * 40);
            var power = Math.Clamp(lit * Seats - i, 0, 1);
            DrawSeat(context, seat, accent, power);
        }

        DrawText(context, _exiting ? ExitTitle : EnterTitle, new Point(bounds.Center.X, origin.Y + Rows * 40 + 18), 14,
            Brush("TextMutedBrush"));
    }

    private void DrawSeat(DrawingContext context, Point seat, Color accent, double power)
    {
        var screen = new Rect(seat.X - ScreenWidth / 2, seat.Y - ScreenHeight / 2, ScreenWidth, ScreenHeight);
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), new Pen(Brush("BorderBrush"), 1), screen, 3, 3);
        if (power > 0)
        {
            context.DrawRectangle(new SolidColorBrush(accent, 0.75 * power), null, screen.Deflate(2.5), 2, 2);
        }
    }

    private void DrawText(DrawingContext context, string text, Point center, double size, IBrush brush)
    {
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, size, brush);
        context.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;

    private Color Color(string key) => Brush(key) is ISolidColorBrush brush ? brush.Color : Colors.Gray;
}
