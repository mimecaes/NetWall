using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace NetWall.IDE.Controls;

public sealed class PageTurn : Control
{
    private const double DurationMilliseconds = 850;
    private const double Lift = 0.1;
    private const double ShadowWidth = 70;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();
    private Bitmap? _old;
    private Bitmap? _new;
    private Rect _left;
    private Rect _right;
    private bool _forward;
    private TaskCompletionSource? _done;

    public PageTurn()
    {
        IsVisible = false;
        _timer.Tick += OnTick;
    }

    public void Cover(Bitmap old, Rect left, Rect right)
    {
        _old = old;
        _new = null;
        _left = left;
        _right = right;
        IsVisible = true;
        InvalidateVisual();
    }

    public Task PlayAsync(Bitmap next, bool forward)
    {
        _new = next;
        _forward = forward;
        _done = new TaskCompletionSource();
        _clock.Restart();
        _timer.Start();
        return _done.Task;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        InvalidateVisual();
        if (_clock.Elapsed.TotalMilliseconds < DurationMilliseconds)
        {
            return;
        }

        _timer.Stop();
        IsVisible = false;
        _old?.Dispose();
        _new?.Dispose();
        _old = null;
        _new = null;
        _done?.TrySetResult();
    }

    public override void Render(DrawingContext context)
    {
        if (_old is null)
        {
            return;
        }

        context.DrawImage(_old, _left, _left);
        context.DrawImage(_old, _right, _right);
        if (_new is null)
        {
            return;
        }

        var k = Math.Clamp(_clock.Elapsed.TotalMilliseconds / DurationMilliseconds, 0, 1);
        var theta = Math.PI * (k < 0.5 ? 2 * k * k : 1 - Math.Pow(-2 * k + 2, 2) / 2);
        var revealed = _forward ? _right : _left;
        context.DrawImage(_new, revealed, revealed);
        var firstHalf = theta < Math.PI / 2;
        var spine = firstHalf == _forward ? _right.Left : _left.Right;
        DrawSpineShadow(context, revealed, _forward ? _right.Left : _left.Right, Math.Sin(theta));

        var face = firstHalf ? _forward ? _right : _left : _forward ? _left : _right;
        var image = firstHalf ? _old : _new;
        var reach = face.Width * Math.Cos(theta);
        var direction = _forward ? 1 : -1;
        var far = spine + direction * reach;
        var grow = face.Height * Lift * Math.Sin(theta) / 2;
        var top = face.Top;
        var bottom = face.Bottom;
        var spineTop = new Point(spine, top);
        var spineBottom = new Point(spine, bottom);
        var farTop = new Point(far, top - grow);
        var farBottom = new Point(far, bottom + grow);

        var underneath = firstHalf ? revealed : _forward ? _left : _right;
        DrawCastShadow(context, underneath, far, firstHalf ? direction : -direction, Math.Sin(theta));

        var leftEdgeAtSpine = (_forward && firstHalf) || (!_forward && !firstHalf);
        var quad = leftEdgeAtSpine
            ? (spineTop, farTop, farBottom, spineBottom)
            : (farTop, spineTop, spineBottom, farBottom);
        var transform = Matrix.CreateScale(1 / face.Width, 1 / face.Height) * QuadMatrix(quad);
        using (context.PushTransform(transform))
        {
            var local = new Rect(face.Size);
            context.DrawImage(image, face, local);
            var turn = firstHalf ? theta / (Math.PI / 2) : (Math.PI - theta) / (Math.PI / 2);
            var shade = 0.42 * (firstHalf ? turn : turn * 0.8);
            context.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(leftEdgeAtSpine ? 0 : 1, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(leftEdgeAtSpine ? 1 : 0, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb((byte)(255 * shade * 0.35), 0, 0, 0), 0),
                    new GradientStop(Color.FromArgb((byte)(255 * shade), 0, 0, 0), 1),
                },
            }, null, local);
        }
    }

    private static void DrawSpineShadow(DrawingContext context, Rect page, double spine, double strength)
    {
        var width = 34.0;
        var nearLeft = Math.Abs(page.Left - spine) < Math.Abs(page.Right - spine);
        var rect = nearLeft ? new Rect(page.Left, page.Top, width, page.Height)
            : new Rect(page.Right - width, page.Top, width, page.Height);
        var alpha = (byte)(255 * 0.28 * strength);
        context.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(nearLeft ? 0 : 1, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(nearLeft ? 1 : 0, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(alpha, 0, 0, 0), 0), new GradientStop(Colors.Transparent, 1) },
        }, null, rect);
    }

    private static void DrawCastShadow(DrawingContext context, Rect page, double edge, int direction, double strength)
    {
        var width = ShadowWidth * strength;
        if (width < 1)
        {
            return;
        }

        var from = direction > 0 ? edge : edge - width;
        var rect = new Rect(from, page.Top, width, page.Height).Intersect(page);
        if (rect.Width <= 0)
        {
            return;
        }

        var alpha = (byte)(255 * 0.38 * strength);
        using var clip = context.PushClip(page);
        context.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(direction > 0 ? 0 : 1, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(direction > 0 ? 1 : 0, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(alpha, 0, 0, 0), 0), new GradientStop(Colors.Transparent, 1) },
        }, null, new Rect(from, page.Top, width, page.Height));
    }

    private static Matrix QuadMatrix((Point TopLeft, Point TopRight, Point BottomRight, Point BottomLeft) quad)
    {
        var (p0, p1, p2, p3) = quad;
        var dx1 = p1.X - p2.X;
        var dx2 = p3.X - p2.X;
        var dx3 = p0.X - p1.X + p2.X - p3.X;
        var dy1 = p1.Y - p2.Y;
        var dy2 = p3.Y - p2.Y;
        var dy3 = p0.Y - p1.Y + p2.Y - p3.Y;
        var denominator = dx1 * dy2 - dx2 * dy1;
        var g = Math.Abs(denominator) < 1e-9 ? 0 : (dx3 * dy2 - dx2 * dy3) / denominator;
        var h = Math.Abs(denominator) < 1e-9 ? 0 : (dx1 * dy3 - dx3 * dy1) / denominator;
        var a = p1.X - p0.X + g * p1.X;
        var b = p3.X - p0.X + h * p3.X;
        var d = p1.Y - p0.Y + g * p1.Y;
        var e = p3.Y - p0.Y + h * p3.Y;
        return new Matrix(a, d, g, b, e, h, p0.X, p0.Y, 1);
    }
}
