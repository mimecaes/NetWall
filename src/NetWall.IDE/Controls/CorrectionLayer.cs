using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NetWall.IDE.Models;

namespace NetWall.IDE.Controls;

public sealed class CorrectionLayer : Control
{
    private const int Cell = 32;
    private const int WalkRightRow = 6;
    private const int WalkLeftRow = 7;
    private const int HappyRow = 41;
    private const int IdleRow = 52;
    private const double HelpySize = 42;
    private const double FadeMilliseconds = 300;

    private static readonly FontFamily Handwriting = new("avares://NetWall.IDE/Assets/Fonts#Caveat");

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<(Func<(Rect Box, Rect Row)> Target, bool Passed, double Start, double Duration)> _marks = [];
    private (string Text, bool Success, Func<Point> Anchor, double Start, double Duration)? _note;
    private Bitmap _sheet = PetWidget.LoadSheet(PetColor.Gray);
    private Point _from;
    private Point _to;
    private double _walkStart;
    private double _walkDuration;
    private bool _facingRight = true;
    private bool _happy;
    private double? _helpyShown;
    private double? _helpyHidden;

    public CorrectionLayer()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        _timer.Tick += (_, _) =>
        {
            InvalidateVisual();
            if (_helpyHidden is { } hidden && Now - hidden > FadeMilliseconds)
            {
                _helpyShown = null;
                _helpyHidden = null;
                _timer.Stop();
            }
        };
    }

    private double Now => _clock.Elapsed.TotalMilliseconds;

    public void SetHelpyColor(PetColor color) => _sheet = PetWidget.LoadSheet(color);

    public void Clear()
    {
        _marks.Clear();
        _note = null;
        _helpyShown = null;
        _helpyHidden = null;
        _timer.Stop();
        InvalidateVisual();
    }

    public void ShowHelpy(Point at)
    {
        _from = _to = at;
        _walkDuration = 0;
        _happy = false;
        _helpyShown = Now;
        _helpyHidden = null;
        _timer.Start();
    }

    public Task WalkToAsync(Point target, double milliseconds)
    {
        _from = HelpyPosition();
        _to = target;
        _facingRight = target.X >= _from.X;
        _walkStart = Now;
        _walkDuration = milliseconds;
        return Task.Delay((int)milliseconds);
    }

    public Task MarkAsync(Func<(Rect Box, Rect Row)> target, bool passed, double milliseconds)
    {
        _marks.Add((target, passed, Now, milliseconds));
        return Task.Delay((int)milliseconds);
    }

    public Task NoteAsync(string text, bool success, Func<Point> anchor, double milliseconds)
    {
        _happy = success;
        _note = (text, success, anchor, Now, milliseconds);
        return Task.Delay((int)milliseconds);
    }

    public Task HideHelpyAsync()
    {
        _helpyHidden = Now;
        return Task.Delay((int)FadeMilliseconds);
    }

    public override void Render(DrawingContext context)
    {
        foreach (var mark in _marks)
        {
            var (box, row) = mark.Target();
            DrawMark(context, box, row, mark.Passed, Progress(mark.Start, mark.Duration));
        }

        if (_note is { } note)
        {
            DrawNote(context, note.Text, note.Success, note.Anchor(), Progress(note.Start, note.Duration));
        }

        if (_helpyShown is not null)
        {
            DrawHelpy(context);
        }
    }

    private double Progress(double start, double duration) =>
        duration <= 0 ? 1 : Math.Clamp((Now - start) / duration, 0, 1);

    private Point HelpyPosition()
    {
        var k = Progress(_walkStart, _walkDuration);
        var eased = k < 0.5 ? 2 * k * k : 1 - Math.Pow(-2 * k + 2, 2) / 2;
        return _from + (_to - _from) * eased;
    }

    private void DrawHelpy(DrawingContext context)
    {
        var fade = _helpyHidden is { } hidden ? 1 - Math.Clamp((Now - hidden) / FadeMilliseconds, 0, 1)
            : Math.Clamp((Now - _helpyShown!.Value) / FadeMilliseconds, 0, 1);
        using var opacity = context.PushOpacity(fade);
        var walking = Progress(_walkStart, _walkDuration) < 1;
        var position = HelpyPosition();
        var (row, frames, speed) = walking ? (_facingRight ? WalkRightRow : WalkLeftRow, 8, 85.0)
            : _happy ? (HappyRow, 2, 160.0)
            : (IdleRow, 4, 420.0);
        var frame = (int)(Now / speed) % frames;
        var bob = walking ? -Math.Abs(Math.Sin(Now / 90)) * 2 : 0;
        var target = new Rect(position.X - HelpySize / 2, position.Y - HelpySize + 6 + bob, HelpySize, HelpySize);
        context.DrawImage(_sheet, new Rect(frame * Cell, row * Cell, Cell, Cell), target);

        var side = _facingRight ? 1 : -1;
        var grip = position + new Vector(side * 12, -14 + bob);
        var tip = grip + new Vector(side * 9, 11);
        context.DrawLine(new Pen(Brush("PastelPinkBrush"), 3.2, lineCap: PenLineCap.Round), grip, tip);
        context.DrawLine(new Pen(Brush("NotebookInkBrush"), 1.6, lineCap: PenLineCap.Round),
            tip - new Vector(side * 2.4, 2.9), tip);
    }

    private void DrawMark(DrawingContext context, Rect box, Rect row, bool passed, double k)
    {
        var pen = new Pen(Brush(passed ? "InkMintBrush" : "InkPinkBrush"), passed ? 3.2 : 2.4,
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        if (passed)
        {
            DrawPartial(context,
            [
                new Point(box.Left + 1, box.Center.Y),
                new Point(box.Center.X, box.Bottom + 1),
                new Point(box.Right + 9, box.Top - 9),
            ], k, pen);
            return;
        }

        var ring = Enumerable.Range(0, 49).Select(i =>
        {
            var angle = -1.9 + i / 48.0 * Math.PI * 2.15;
            var wobble = 1 + 0.04 * Math.Sin(i * 0.9);
            return row.Center + new Vector(Math.Cos(angle) * (row.Width / 2 + 8) * wobble,
                Math.Sin(angle) * (row.Height / 2 + 6) * wobble);
        }).ToArray();
        DrawPartial(context, ring, Math.Clamp(k / 0.65, 0, 1), pen);
        DrawPartial(context, [box.TopLeft, box.BottomRight], Math.Clamp((k - 0.65) / 0.17, 0, 1), pen);
        DrawPartial(context, [box.TopRight, box.BottomLeft], Math.Clamp((k - 0.82) / 0.18, 0, 1), pen);
    }

    private void DrawNote(DrawingContext context, string text, bool success, Point anchor, double k)
    {
        var brush = Brush(success ? "InkMintBrush" : "InkPinkBrush");
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Handwriting, FontStyle.Normal, FontWeight.Bold), 28, brush);
        var origin = new Point(anchor.X - formatted.Width, anchor.Y - formatted.Height);
        using var rotate = context.PushTransform(Matrix.CreateTranslation(-anchor.X, -anchor.Y)
                                                 * Matrix.CreateRotation(-0.1)
                                                 * Matrix.CreateTranslation(anchor.X, anchor.Y));
        using (context.PushClip(new Rect(origin.X - 4, origin.Y - 6, (formatted.Width + 8) * Math.Clamp(k / 0.75, 0, 1),
                   formatted.Height + 12)))
        {
            context.DrawText(formatted, origin);
        }

        var line = Enumerable.Range(0, 25).Select(i => new Point(origin.X + i / 24.0 * formatted.Width,
            anchor.Y + 2 + Math.Sin(i * 0.8) * 2)).ToArray();
        DrawPartial(context, line, Math.Clamp((k - 0.7) / 0.3, 0, 1), new Pen(brush, 2, lineCap: PenLineCap.Round));
    }

    private static void DrawPartial(DrawingContext context, Point[] points, double k, IPen pen)
    {
        if (k <= 0 || points.Length < 2)
        {
            return;
        }

        var lengths = points.Zip(points.Skip(1), (a, b) => Point.Distance(a, b)).ToArray();
        var remaining = lengths.Sum() * k;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(points[0], false);
            for (var i = 0; i < lengths.Length && remaining > 0; i++)
            {
                var step = Math.Min(1, remaining / lengths[i]);
                path.LineTo(points[i] + (points[i + 1] - points[i]) * step);
                remaining -= lengths[i];
            }

            path.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;
}
