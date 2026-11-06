using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NetWall.IDE.Models;
using NetWall.IDE.Services;
using NetWall.IDE.Themes;

namespace NetWall.IDE.Controls;

public enum BubbleTone
{
    Neutral,
    Allowed,
    Blocked,
}

public sealed class SimulationCanvas : Control
{
    private const double Inset = 16;
    private const double CellWidth = 96;
    private const double CellHeight = 78;
    private const double IconWidth = 40;
    private const double IconHeight = 32;
    private const double FirstRing = 190;
    private const double RingGap = 116;
    private const double RowSpacing = 84;
    private const double CorridorSpacing = 118;
    private const int MaxFirstRing = 5;
    private const double MinScale = 0.45;
    private const double MaxScale = 1.15;
    private const double MaxStretchX = 2.4;
    private const double MaxStretchY = 1.7;
    private const double WallWidth = 58;
    private const double WallMinHeight = 110;
    private const double WallMaxHeight = 230;
    private const double HelpySize = 64;
    private const double HelpySpace = 122;
    private const double WallFooter = 54;
    private const double BrickHeight = 12;
    private const double BrickWidth = 24;
    private const double Mortar = 2;
    private const int SpriteCell = 32;
    private const int TrailLength = 6;
    private const string WallKey = "\0wall";
    private const double BuilderMinWidth = 700;
    private const double PortRadius = 6;
    private const double DeviceGrow = 0.22;
    private const double WallGrow = 0.06;
    private const double GrowMilliseconds = 90;

    private static readonly FontFamily Font = new("Cascadia Code,Consolas,DejaVu Sans Mono,monospace");
    internal static readonly string[] ZoneBrushes = ["Ansi5Brush", "Ansi6Brush", "Ansi3Brush", "Ansi4Brush"];
    private static readonly double[] Shifts = [0, 0.5, -0.5, 0.25, -0.25];

    private readonly List<Host> _left = [];
    private readonly List<Host> _right = [];
    private readonly Dictionary<string, Device> _devices = [];
    private readonly Dictionary<string, (int Sent, int Received, int Blocked)> _stats = [];
    private readonly List<(Func<Point> Center, string Brush, long Start)> _pulses = [];
    private readonly Dictionary<string, double> _grow = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stopwatch _realClock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    private SimulationResult? _result;
    private double _scale = 1;
    private double _stretchX = 1;
    private double _stretchY = 1;
    private Size _design;
    private Rect _wall;
    private (Func<IReadOnlyList<Point>> Path, long Start, double Duration)? _motion;
    private string _packetBrush = "AccentBrush";
    private Func<IReadOnlyList<Point>>? _route;
    private string _routeBrush = "AccentBrush";
    private string? _decision;
    private bool _allowed;
    private long? _inspectStart;
    private SimulatedPacket? _door;
    private long? _doorOpened;
    private long? _doorClosed;
    private long? _shakeStart;
    private string? _activeSource;
    private string? _activeDestination;
    private string? _hover;
    private Point _hoverPoint;
    private long _lastIdleFrame;
    private long _lastTick;

    private ObservableCollection<BuilderDevice>? _builder;
    private BuilderDevice? _dragging;
    private Vector _dragOffset;
    private Point _pressPoint;
    private bool _dragMoved;
    private BuilderDevice? _linking;
    private Point _linkEnd;

    private bool _helpyEnabled = true;
    private Bitmap _sheet = PetWidget.LoadSheet(PetColor.Gray);
    private Mood _mood = Mood.Idle;
    private string _bubble = string.Empty;
    private long _bubbleStart;
    private BubbleTone _bubbleTone;
    private bool _helpyHopping;

    public SimulationCanvas()
    {
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        _timer.Tick += OnTick;
    }

    private enum Mood
    {
        Idle,
        Talking,
        Happy,
    }

    public SimulationResult? Result
    {
        get => _result;
        set
        {
            _result = value;
            UpdateTimer();
            BuildHosts();
            Clear();
            _bubble = string.Empty;
            InvalidateArrange();
        }
    }

    public event Action<BuilderDevice>? DeviceClicked;

    public event Action<BuilderDevice>? DeviceConnected;

    public event Action<BuilderDevice, BuilderDevice>? PacketDragged;

    public ObservableCollection<BuilderDevice>? Builder
    {
        get => _builder;
        set
        {
            if (_builder is not null)
            {
                _builder.CollectionChanged -= OnBuilderChanged;
                foreach (var device in _builder)
                {
                    device.PropertyChanged -= OnBuilderDeviceChanged;
                }
            }

            _builder = value;
            if (_builder is not null)
            {
                _builder.CollectionChanged += OnBuilderChanged;
                foreach (var device in _builder)
                {
                    device.PropertyChanged += OnBuilderDeviceChanged;
                }
            }

            InvalidateArrange();
        }
    }

    public Point Normalize(Point position) =>
        new(Math.Clamp(position.X / _scale / _design.Width, 0, 1), Math.Clamp(position.Y / _scale / _design.Height, 0, 1));

    public void BeginCarry(BuilderDevice device, PointerPressedEventArgs e)
    {
        _dragging = device;
        _dragOffset = default;
        _dragMoved = true;
        e.Pointer.Capture(this);
    }

    public bool Paused
    {
        get => !_clock.IsRunning;
        set
        {
            if (value)
            {
                _clock.Stop();
            }
            else
            {
                _clock.Start();
            }
        }
    }

    public async Task WaitAsync(double milliseconds, CancellationToken token)
    {
        var started = _clock.ElapsedMilliseconds;
        while (_clock.ElapsedMilliseconds - started < milliseconds)
        {
            await Task.Delay(15, token);
        }
    }

    public bool HelpyEnabled
    {
        get => _helpyEnabled;
        set
        {
            _helpyEnabled = value;
            InvalidateArrange();
            InvalidateVisual();
        }
    }

    public void SetHelpyColor(PetColor color)
    {
        _sheet = PetWidget.LoadSheet(color);
        InvalidateVisual();
    }

    public void Clear()
    {
        _motion = null;
        _route = null;
        _decision = null;
        _inspectStart = null;
        _helpyHopping = false;
        _door = null;
        _doorOpened = null;
        _doorClosed = null;
        _shakeStart = null;
        _activeSource = null;
        _activeDestination = null;
        _pulses.Clear();
        _mood = Mood.Idle;
        InvalidateVisual();
    }

    public Task SayAsync(string text, BubbleTone tone = BubbleTone.Neutral)
    {
        if (!_helpyEnabled)
        {
            return Task.CompletedTask;
        }

        _bubble = text;
        _bubbleTone = tone;
        _bubbleStart = _clock.ElapsedMilliseconds;
        _mood = Mood.Talking;
        var typing = TypingMilliseconds(text);
        _ = RestAfterTypingAsync(_bubbleStart, typing);
        return WaitAsync(typing, CancellationToken.None);
    }

    public void Begin(SimulatedPacket packet)
    {
        Clear();
        _activeSource = packet.Source;
        _packetBrush = "AccentBrush";
        _routeBrush = "AccentBrush";
        _route = () => PathToWall(packet);
    }

    public void BeginInspect()
    {
        _inspectStart = _clock.ElapsedMilliseconds;
        _helpyHopping = true;
    }

    public void EndInspect()
    {
        _inspectStart = null;
        _helpyHopping = false;
    }

    public void ShowDecision(SimulatedPacket packet)
    {
        _allowed = packet.IsAllowed;
        _decision = packet.Rule is null ? "política por defecto" : $"regla {packet.Rule}";
        _packetBrush = _allowed ? "RunGreenBrush" : "StopRedBrush";
        _routeBrush = _packetBrush;
        if (_allowed)
        {
            _route = () => [.. PathToWall(packet), .. PathFromWall(packet)];
            _activeDestination = packet.Destination;
            _door = packet;
            _doorOpened = _clock.ElapsedMilliseconds;
            _doorClosed = null;
            _mood = Mood.Happy;
        }
        else
        {
            _shakeStart = _clock.ElapsedMilliseconds;
            _pulses.Add((() => WallPoint(packet.Source), "StopRedBrush", _clock.ElapsedMilliseconds));
        }

        InvalidateVisual();
    }

    public void Arrive(SimulatedPacket packet)
    {
        if (packet.IsAllowed)
        {
            _pulses.Add((() => DeviceCenter(packet.Destination), "RunGreenBrush", _clock.ElapsedMilliseconds));
        }

        _doorClosed = _clock.ElapsedMilliseconds;
    }

    public void HidePacket()
    {
        _motion = null;
        _route = null;
        _activeSource = null;
        _activeDestination = null;
        InvalidateVisual();
    }

    public void ShowRoute(SimulatedPacket packet)
    {
        Clear();
        _allowed = packet.IsAllowed;
        _decision = packet.Rule is null ? "política por defecto" : $"regla {packet.Rule}";
        _routeBrush = _allowed ? "RunGreenBrush" : "StopRedBrush";
        _route = _allowed ? () => [.. PathToWall(packet), .. PathFromWall(packet)] : () => PathToWall(packet);
        _activeSource = packet.Source;
        _activeDestination = _allowed ? packet.Destination : null;
    }

    public IReadOnlyList<Point> PathToWall(SimulatedPacket packet) => Path(packet.Source, toWall: true);

    public IReadOnlyList<Point> PathFromWall(SimulatedPacket packet) => Path(packet.Destination, toWall: false);

    public IReadOnlyList<Point> BouncePath(SimulatedPacket packet)
    {
        var path = Path(packet.Source, toWall: true);
        return [path[^1], path[^1] + (path[0] - path[^1]) * 0.3];
    }

    public async Task TravelAsync(Func<IReadOnlyList<Point>> path, double speed, CancellationToken token)
    {
        var duration = Math.Clamp(Length(path()) * 3.2, 450, 1500) / speed;
        var started = _clock.ElapsedMilliseconds;
        _motion = (path, started, duration);
        while (_clock.ElapsedMilliseconds - started < duration)
        {
            await Task.Delay(15, token);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ThemeManager.ThemeApplied += OnThemeApplied;
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ThemeManager.ThemeApplied -= OnThemeApplied;
        _timer.Stop();
    }

    private void UpdateTimer() => _timer.IsEnabled = _result is not null && VisualRoot is not null;

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_builder is not null)
        {
            ArrangeBuilder(finalSize);
            return finalSize;
        }

        var (left, right, reach, radius, above, below, scale) = Enumerable.Range(1, MaxFirstRing)
            .Select(capacity => Fit(finalSize, capacity))
            .MaxBy(fit => fit.Scale);
        var requiredHeight = above + below + 2 * Inset;
        _scale = Math.Clamp(scale, MinScale, MaxScale);
        _design = new Size(finalSize.Width / _scale, finalSize.Height / _scale);

        var extra = Math.Max(0, _design.Height - requiredHeight);
        var center = new Point(_design.Width / 2, Inset + above + extra * above / (above + below));
        var room = Math.Min(center.Y - Inset, _design.Height - center.Y - Inset) - CellHeight / 2;
        var spread = reach - CellHeight / 2;
        _stretchY = spread > 0 ? Math.Clamp(room / spread, 1, MaxStretchY) : 1;
        _stretchX = Math.Clamp((_design.Width / 2 - Inset - CellWidth / 2) / (radius - CellWidth / 2), 1, MaxStretchX);

        var wallHeight = Math.Clamp(Math.Min(2 * spread * _stretchY + CellHeight, 2 * (center.Y - Inset - TopSpace)),
            WallMinHeight, WallMaxHeight);
        _wall = new Rect(center.X - WallWidth / 2, center.Y - wallHeight / 2, WallWidth, wallHeight);

        _devices.Clear();
        PlaceSide(left, center, isLeft: true);
        PlaceSide(right, center, isLeft: false);
        return finalSize;
    }

    private (List<List<Host>> Left, List<List<Host>> Right, double Reach, double Radius, double Above, double Below,
        double Scale) Fit(Size size, int capacity)
    {
        var left = Rings(_left, capacity);
        var right = Rings(_right, capacity);
        var reach = Math.Max(Reach(left), Reach(right));
        var rings = Math.Max(left.Count, right.Count);
        var radius = (rings == 0 ? FirstRing : RingRadius(rings - 1)) + CellWidth / 2;
        var above = Math.Max(reach, TopSpace + WallMinHeight / 2);
        var below = Math.Max(reach, WallMinHeight / 2 + WallFooter);
        var scale = Math.Min(size.Height / (above + below + 2 * Inset), size.Width / (2 * (radius + Inset)));
        return (left, right, reach, radius, above, below, scale);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = DesignPoint(e.GetPosition(this));
        if (_builder is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_devices.Values.FirstOrDefault(device => device.Model is not null && Distance(Port(device), point) <= PortRadius + 5)
            is { Model: { } linking })
        {
            _linking = linking;
            _linkEnd = point;
        }
        else if (DeviceAt(point) is { Model: { } model } device)
        {
            _dragging = model;
            _dragOffset = device.Cell.Center - point;
            _dragMoved = false;
            _pressPoint = point;
        }
        else
        {
            return;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = DesignPoint(e.GetPosition(this));
        if (_linking is { } linking)
        {
            if (_wall.Inflate(14).Contains(point))
            {
                linking.IsConnected = true;
                DeviceConnected?.Invoke(linking);
            }
            else if (DeviceAt(point) is { Model: { } target } && target != linking)
            {
                PacketDragged?.Invoke(linking, target);
            }

            _linking = null;
            InvalidateVisual();
        }
        else if (_dragging is { } dragging)
        {
            _dragging = null;
            if (!_dragMoved)
            {
                DeviceClicked?.Invoke(dragging);
            }
        }
        else
        {
            return;
        }

        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        var point = DesignPoint(position);
        if (_dragging is { } dragging)
        {
            _dragMoved |= Distance(point, _pressPoint) > 4;
            var center = point + _dragOffset;
            dragging.X = Math.Clamp(center.X / _design.Width, 0, 1);
            dragging.Y = Math.Clamp(center.Y / _design.Height, 0, 1);
            return;
        }

        if (_linking is not null)
        {
            _linkEnd = point;
            InvalidateVisual();
            return;
        }

        var hover = DeviceAt(point)?.Key ?? (_wall.Inflate(6).Contains(point) ? WallKey : null);
        if (hover != _hover || hover is not null)
        {
            _hover = hover;
            _hoverPoint = point;
            Cursor = new Cursor(hover is null ? StandardCursorType.Arrow : StandardCursorType.Hand);
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_result is null)
        {
            return;
        }

        using var scale = context.PushTransform(Matrix.CreateScale(_scale, _scale));
        var now = _clock.ElapsedMilliseconds;

        DrawWires(context);
        DrawLink(context);
        DrawRoute(context, now);
        foreach (var device in _devices.Values.OrderBy(device => Grow(device.Key)))
        {
            DrawDevice(context, device);
        }

        DrawWall(context, now);
        DrawPacket(context, now);
        DrawPulses(context, now);

        if (_helpyEnabled)
        {
            DrawHelpy(context, now);
        }

        DrawTooltip(context);
    }

    private double TopSpace => _helpyEnabled ? HelpySpace : 24;

    private void OnThemeApplied(object? sender, EventArgs e) => InvalidateVisual();

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.ElapsedMilliseconds;
        var real = _realClock.ElapsedMilliseconds;
        var growing = UpdateGrow(real - _lastTick);
        _lastTick = real;
        _pulses.RemoveAll(pulse => now - pulse.Start > 700);
        var busy = _motion is not null || _route is not null || _inspectStart is not null || _pulses.Count > 0
                   || _shakeStart is { } shake && now - shake < 600
                   || _doorOpened is not null
                   || now - _bubbleStart < TypingMilliseconds(_bubble) + 100;
        if (busy || growing || real - _lastIdleFrame > 140)
        {
            _lastIdleFrame = real;
            InvalidateVisual();
        }
    }

    private bool UpdateGrow(long elapsed)
    {
        var step = 1 - Math.Exp(-elapsed / GrowMilliseconds);
        var changed = false;
        foreach (var key in _devices.Keys.Append(WallKey))
        {
            var current = Grow(key);
            var target = key == _hover ? 1 : 0;
            if (Math.Abs(target - current) < 0.002)
            {
                _grow[key] = target;
                continue;
            }

            _grow[key] = current + (target - current) * step;
            changed = true;
        }

        return changed;
    }

    private double Grow(string key) => _grow.GetValueOrDefault(key);

    private Point DesignPoint(Point position) => new(position.X / _scale, position.Y / _scale);

    private Device? DeviceAt(Point point) =>
        _devices.Values.LastOrDefault(device => device.Cell.Deflate(new Thickness(10, 2)).Contains(point));

    private Point Port(Device device)
    {
        var towardWall = device.Icon.Center.X < _wall.Center.X ? 1 : -1;
        return device.Icon.Center + new Vector(towardWall * (IconWidth / 2 + 10), 0);
    }

    private void OnBuilderChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (BuilderDevice device in e.NewItems ?? Array.Empty<BuilderDevice>())
        {
            device.PropertyChanged += OnBuilderDeviceChanged;
        }

        foreach (BuilderDevice device in e.OldItems ?? Array.Empty<BuilderDevice>())
        {
            device.PropertyChanged -= OnBuilderDeviceChanged;
        }

        InvalidateArrange();
    }

    private void OnBuilderDeviceChanged(object? sender, PropertyChangedEventArgs e) => InvalidateArrange();

    private void ArrangeBuilder(Size size)
    {
        var required = TopSpace + WallMinHeight + WallFooter + 2 * Inset;
        _scale = Math.Clamp(Math.Min(size.Height / required, size.Width / BuilderMinWidth), 0.6, 1);
        _design = new Size(size.Width / _scale, size.Height / _scale);
        var wallHeight = Math.Clamp(_design.Height - TopSpace - WallFooter - 2 * Inset, WallMinHeight, WallMaxHeight);
        var center = new Point(_design.Width / 2,
            Math.Max(Inset + TopSpace + wallHeight / 2, (_design.Height - WallFooter + TopSpace) / 2));
        _wall = new Rect(center.X - WallWidth / 2, center.Y - wallHeight / 2, WallWidth, wallHeight);

        var zones = (_result?.Networks ?? [])
            .Where(network => Ipv4.Parse(network.Cidr) is { Mask: < 32 and > 0 })
            .OrderByDescending(network => Ipv4.Parse(network.Cidr)!.Value.Mask)
            .ToList();
        _devices.Clear();
        var placed = new List<(BuilderDevice Model, Point Position)>();
        foreach (var model in _builder!)
        {
            var position = new Point(
                Math.Clamp(model.X * _design.Width, CellWidth / 2, _design.Width - CellWidth / 2),
                Math.Clamp(model.Y * _design.Height, CellHeight / 2, _design.Height - CellHeight / 2));
            placed.Add((model, position));
        }

        foreach (var side in placed.GroupBy(item => item.Position.X < center.X))
        {
            var connected = side.Where(item => item.Model.IsConnected).OrderBy(item => item.Position.Y).ToList();
            foreach (var (model, position) in side)
            {
                var index = connected.FindIndex(item => item.Model == model);
                var wallY = index < 0
                    ? Math.Clamp(position.Y, _wall.Top + 12, _wall.Bottom - 12)
                    : _wall.Top + (index + 1) * _wall.Height / (connected.Count + 1);
                var zone = model.Network?.Network is { } network && zones.Contains(network) ? network : null;
                var host = new Host(model.Address, zone, zone is null ? -1 : zones.IndexOf(zone));
                var cell = new Rect(position.X - CellWidth / 2, position.Y - CellHeight / 2, CellWidth, CellHeight);
                var icon = new Rect(cell.Center.X - IconWidth / 2, cell.Y + 20, IconWidth, IconHeight);
                var name = zone is null && model.Network?.Network is { } named ? named.Name : null;
                var key = Ipv4.Parse(model.Address) is null || _devices.ContainsKey(model.Address)
                    ? $"#{model.Id}"
                    : model.Address;
                _devices[key] = new Device(host, name, cell, icon, model.Kind,
                    new Point(side.Key ? _wall.Left : _wall.Right, wallY), key, model);
            }
        }
    }

    private static Matrix ScaleAround(Point center, double scale) =>
        Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale)
        * Matrix.CreateTranslation(center.X, center.Y);

    private async Task RestAfterTypingAsync(long started, int typing)
    {
        await WaitAsync(typing + 200, CancellationToken.None);
        if (_bubbleStart == started && _mood == Mood.Talking)
        {
            _mood = _bubbleTone == BubbleTone.Allowed ? Mood.Happy : Mood.Idle;
        }
    }

    private (int Row, int Frames, int Milliseconds) HelpyAnimation => _mood switch
    {
        Mood.Talking => (32, 8, 90),
        Mood.Happy => (41, 2, 160),
        _ => (52, 4, 420),
    };

    private static int TypingMilliseconds(string text) => text.Length * 16;

    private void BuildHosts()
    {
        _left.Clear();
        _right.Clear();
        _stats.Clear();
        if (_result?.Packets is not { } packets)
        {
            return;
        }

        foreach (var packet in packets)
        {
            var source = _stats.GetValueOrDefault(packet.Source);
            _stats[packet.Source] = (source.Sent + 1, source.Received, source.Blocked + (packet.IsAllowed ? 0 : 1));
            if (packet.IsAllowed)
            {
                var destination = _stats.GetValueOrDefault(packet.Destination);
                _stats[packet.Destination] = (destination.Sent, destination.Received + 1, destination.Blocked);
            }
        }

        var zones = (_result.Networks ?? [])
            .Select(network => (Network: network, Parsed: Ipv4.Parse(network.Cidr)))
            .Where(zone => zone.Parsed is { Mask: < 32 and > 0 })
            .OrderByDescending(zone => zone.Parsed!.Value.Mask)
            .ToList();

        var leftAddresses = packets.Select(packet => packet.Source).Distinct().ToList();
        var rightAddresses = packets.Select(packet => packet.Destination).Distinct().Except(leftAddresses).ToList();
        _left.AddRange(Hosts(leftAddresses));
        _right.AddRange(Hosts(rightAddresses));

        IEnumerable<Host> Hosts(List<string> addresses) => addresses
            .Select(address =>
            {
                var host = Ipv4.Parse(address);
                var zone = host is null ? null
                    : zones.FirstOrDefault(zone => Ipv4.Contains(zone.Parsed!.Value, host.Value.Base)).Network;
                return new Host(address, zone, zones.FindIndex(candidate => candidate.Network == zone));
            })
            .OrderBy(host => host.ColorIndex < 0 ? int.MaxValue : host.ColorIndex);
    }

    private static List<List<Host>> Rings(List<Host> hosts, int capacity)
    {
        var rings = new List<List<Host>>();
        for (var index = 0; index < hosts.Count; index += rings[^1].Count)
        {
            rings.Add(hosts.Skip(index).Take(capacity + rings.Count).ToList());
        }

        return rings;
    }

    private static double RingRadius(int ring) => FirstRing + ring * RingGap;

    private static double Spacing(List<List<Host>> rings) => rings.Count > 1 ? CorridorSpacing : RowSpacing;

    private static double Offset(int index, int count, double shift, double spacing) =>
        (index - (count - 1) / 2.0 + shift) * spacing;

    private static double Reach(List<List<Host>> rings) => rings.Count == 0
        ? 0
        : rings.Max(ring => (ring.Count - 1) / 2.0 + 0.5) * Spacing(rings) + CellHeight / 2;

    private void PlaceSide(List<List<Host>> rings, Point center, bool isLeft)
    {
        var free = Math.Min(Math.Max(0, rings.Count - 1), 4);
        List<(Host Host, Point Position, Point Wall)>? best = null;
        var bestClearance = double.MinValue;
        for (var combination = 0; combination < Math.Pow(Shifts.Length, free); combination++)
        {
            var layout = Layout(rings, center, isLeft, combination);
            var clearance = Clearance(layout);
            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                best = layout;
            }
        }

        foreach (var (host, position, wall) in best ?? [])
        {
            var cell = new Rect(position.X - CellWidth / 2, position.Y - CellHeight / 2, CellWidth, CellHeight);
            var icon = new Rect(cell.Center.X - IconWidth / 2, cell.Y + 20, IconWidth, IconHeight);
            var name = NetworkName(host.Address);
            var kind = name is not null ? DeviceKind.Server
                : host.Zone is not null ? DeviceKind.Computer
                : DeviceKind.Internet;
            _devices[host.Address] = new Device(host, name, cell, icon, kind, wall, host.Address);
        }
    }

    private List<(Host Host, Point Position, Point Wall)> Layout(List<List<Host>> rings, Point center, bool isLeft,
        int combination)
    {
        var placed = new List<(Host Host, Point Position)>();
        for (var ring = 0; ring < rings.Count; ring++)
        {
            var shift = ring == 0 ? 0 : Shifts[combination % Shifts.Length];
            if (ring > 0)
            {
                combination /= Shifts.Length;
            }

            var radius = RingRadius(ring);
            for (var i = 0; i < rings[ring].Count; i++)
            {
                var offset = Offset(i, rings[ring].Count, shift, Spacing(rings));
                var dx = Math.Sqrt(Math.Max(0, radius * radius - offset * offset)) * _stretchX;
                placed.Add((rings[ring][i], new Point(center.X + (isLeft ? -dx : dx), center.Y + offset * _stretchY)));
            }
        }

        var ordered = placed.OrderBy(item => item.Position.Y).ToList();
        return ordered.Select((item, index) => (item.Host, item.Position, new Point(isLeft ? _wall.Left : _wall.Right,
            _wall.Top + (index + 1) * _wall.Height / (ordered.Count + 1)))).ToList();
    }

    private static double Clearance(List<(Host Host, Point Position, Point Wall)> layout)
    {
        var clearance = double.MaxValue;
        foreach (var (host, position, wall) in layout)
        {
            foreach (var other in layout.Where(other => other.Host != host))
            {
                clearance = Math.Min(clearance, SegmentDistance(other.Position, position, wall));
            }
        }

        return clearance;
    }

    private static double SegmentDistance(Point point, Point a, Point b)
    {
        var segment = b - a;
        var length = segment.X * segment.X + segment.Y * segment.Y;
        var t = length == 0 ? 0 : Math.Clamp(((point.X - a.X) * segment.X + (point.Y - a.Y) * segment.Y) / length, 0, 1);
        return Distance(point, a + segment * t);
    }

    private string? NetworkName(string address) =>
        _result?.Networks?.FirstOrDefault(network => network.Cidr == address)?.Name;

    private List<Point> Path(string address, bool toWall)
    {
        if (!_devices.TryGetValue(address, out var device))
        {
            return [_wall.Center, _wall.Center];
        }

        return toWall ? [device.Icon.Center, device.WallPoint] : [device.WallPoint, device.Icon.Center];
    }

    private Point WallPoint(string address) =>
        _devices.TryGetValue(address, out var device) ? device.WallPoint : _wall.Center;

    private Point DeviceCenter(string address) =>
        _devices.TryGetValue(address, out var device) ? device.Icon.Center : _wall.Center;

    private Color ZoneColor(Host host) => host.Zone is null
        ? ColorOf("AccentBrush")
        : ColorOf(ZoneBrushes[host.ColorIndex % ZoneBrushes.Length]);

    private void DrawWires(DrawingContext context)
    {
        var wire = new Pen(new SolidColorBrush(ColorOf("TextMutedBrush"), 0.35), 1.5);
        foreach (var device in _devices.Values.Where(device => device.Model?.IsConnected ?? true))
        {
            context.DrawLine(wire, device.Icon.Center, device.WallPoint);
        }
    }

    private void DrawLink(DrawingContext context)
    {
        if (_linking is null || _devices.Values.FirstOrDefault(device => device.Model == _linking) is not { } device)
        {
            return;
        }

        var accent = ColorOf("AccentBrush");
        var target = _wall.Inflate(14).Contains(_linkEnd);
        var receiver = DeviceAt(_linkEnd) is { Model: { } model } hit && model != _linking ? hit : null;
        context.DrawLine(new Pen(new SolidColorBrush(accent), 2, new DashStyle([4, 3], 0), PenLineCap.Round),
            Port(device), _linkEnd);
        context.DrawEllipse(new SolidColorBrush(accent, target || receiver is not null ? 0.9 : 0.4), null, _linkEnd, 5, 5);
        if (target)
        {
            context.DrawRectangle(null, new Pen(new SolidColorBrush(accent), 2), _wall.Inflate(6), 6, 6);
        }
        else if (receiver is not null)
        {
            context.DrawRectangle(new SolidColorBrush(accent, 0.12), new Pen(new SolidColorBrush(accent), 2),
                receiver.Cell.Deflate(new Thickness(6, 0)), 10, 10);
            DrawCentered(context, "enviar paquete", 10, new SolidColorBrush(accent), receiver.Cell.Center.X,
                receiver.Cell.Bottom + 2, 120, bold: true);
        }
    }

    private void DrawPort(DrawingContext context, Device device, long now)
    {
        var accent = ColorOf("AccentBrush");
        var center = Port(device);
        if (device.Model!.IsConnected)
        {
            context.DrawEllipse(new SolidColorBrush(accent), null, center, 3.5, 3.5);
            return;
        }

        var pulse = 0.5 + 0.5 * Math.Sin(now / 260.0);
        context.DrawEllipse(Brush("TerminalBackgroundBrush"), new Pen(new SolidColorBrush(accent, 0.5 + 0.5 * pulse), 2),
            center, PortRadius, PortRadius);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(accent, 0.3 * (1 - pulse)), 1.5), center,
            PortRadius + 3 + pulse * 4, PortRadius + 3 + pulse * 4);
    }

    private void DrawRoute(DrawingContext context, long now)
    {
        if (_route?.Invoke() is not { Count: > 1 } route)
        {
            return;
        }

        var color = ColorOf(_routeBrush);
        var glow = new Pen(new SolidColorBrush(color, 0.18), 7, lineCap: PenLineCap.Round);
        var dashes = new Pen(new SolidColorBrush(color), 2, new DashStyle([3, 3], -(now / 40.0) % 6),
            lineCap: PenLineCap.Round);
        for (var i = 0; i < route.Count - 1; i++)
        {
            context.DrawLine(glow, route[i], route[i + 1]);
            context.DrawLine(dashes, route[i], route[i + 1]);
        }
    }

    private void DrawDevice(DrawingContext context, Device device)
    {
        var active = device.Address == _activeSource || device.Address == _activeDestination;
        var stroke = Brush(active ? "AccentBrush" : "TextMutedBrush");
        var tint = ZoneColor(device.Host);
        var backdrop = device.Cell.Deflate(new Thickness(10, 2));
        using var grow = context.PushTransform(ScaleAround(device.Cell.Center, 1 + DeviceGrow * Grow(device.Key)));
        context.DrawRectangle(Brush("TerminalBackgroundBrush"), null, backdrop, 10, 10);
        if (active)
        {
            context.DrawRectangle(new SolidColorBrush(ColorOf("AccentBrush"), 0.08), null, backdrop, 10, 10);
        }

        switch (device.Kind)
        {
            case DeviceKind.Computer:
                DrawComputer(context, device.Icon, stroke, tint);
                break;
            case DeviceKind.Server:
                DrawServer(context, device.Icon, stroke);
                break;
            default:
                DrawGlobe(context, device.Icon, stroke, tint);
                break;
        }

        var title = device.Name ?? device.Host.Zone?.Name
            ?? (device.Kind == DeviceKind.Internet ? "internet" : device.Model?.KindLabel.ToLowerInvariant() ?? "internet");
        DrawCentered(context, title, 11, new SolidColorBrush(tint), device.Cell.Center.X, device.Cell.Y + 3,
            CellWidth - 6, bold: true);
        DrawCentered(context, device.Address, 10,
            Brush(device.Model is { HasError: true } ? "StopRedBrush" : "TextPrimaryBrush"), device.Cell.Center.X,
            device.Icon.Bottom + 4, CellWidth - 6);
        if (device.Model is not null)
        {
            DrawPort(context, device, _clock.ElapsedMilliseconds);
        }
    }

    private void DrawComputer(DrawingContext context, Rect icon, IBrush stroke, Color tint)
    {
        var pen = new Pen(stroke, 1.6);
        var monitor = new Rect(icon.X + 4, icon.Y + 1, icon.Width - 8, icon.Height - 11);
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), pen, monitor, 3, 3);
        context.DrawRectangle(new SolidColorBrush(tint, 0.35), null, monitor.Deflate(3), 1.5, 1.5);
        context.DrawLine(new Pen(new SolidColorBrush(tint), 1.2),
            new Point(monitor.X + 7, monitor.Y + 7), new Point(monitor.X + 18, monitor.Y + 7));
        context.DrawLine(new Pen(new SolidColorBrush(tint, 0.6), 1.2),
            new Point(monitor.X + 7, monitor.Y + 11), new Point(monitor.X + 14, monitor.Y + 11));
        context.DrawLine(pen, new Point(icon.Center.X, monitor.Bottom), new Point(icon.Center.X, icon.Bottom - 3));
        context.DrawLine(new Pen(stroke, 2.2, lineCap: PenLineCap.Round),
            new Point(icon.Center.X - 8, icon.Bottom - 2), new Point(icon.Center.X + 8, icon.Bottom - 2));
    }

    private void DrawServer(DrawingContext context, Rect icon, IBrush stroke)
    {
        var pen = new Pen(stroke, 1.6);
        var body = new Rect(icon.Center.X - 12, icon.Y, 24, icon.Height);
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), pen, body, 3, 3);
        for (var i = 0; i < 3; i++)
        {
            var y = body.Y + 7 + i * 9;
            context.DrawLine(new Pen(stroke, 1.2), new Point(body.X + 9, y), new Point(body.Right - 4, y));
            context.DrawEllipse(Brush(i == 0 ? "RunGreenBrush" : "Ansi3Brush"), null, new Point(body.X + 5, y), 1.6, 1.6);
        }
    }

    private static void DrawGlobe(DrawingContext context, Rect icon, IBrush stroke, Color tint)
    {
        var pen = new Pen(stroke, 1.5);
        var thin = new Pen(stroke, 1.1);
        var center = icon.Center;
        var radius = icon.Height / 2 - 1;
        var chord = radius * 0.87;
        context.DrawEllipse(new SolidColorBrush(tint, 0.14), pen, center, radius, radius);
        context.DrawEllipse(null, thin, center, radius * 0.45, radius);
        context.DrawLine(thin, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
        context.DrawLine(thin, new Point(center.X - chord, center.Y - radius / 2),
            new Point(center.X + chord, center.Y - radius / 2));
        context.DrawLine(thin, new Point(center.X - chord, center.Y + radius / 2),
            new Point(center.X + chord, center.Y + radius / 2));
    }

    private void DrawWall(DrawingContext context, long now)
    {
        var shake = _shakeStart is { } started && now - started < 600
            ? Math.Sin((now - started) / 22.0) * 4 * (1 - (now - started) / 600.0)
            : 0;
        var wall = _wall.Translate(new Vector(shake, 0));
        using var grow = context.PushTransform(ScaleAround(_wall.Center, 1 + WallGrow * Grow(WallKey)));
        var door = DoorAmount(now);
        var doorY = _door is { } packet ? (WallPoint(packet.Source).Y + WallPoint(packet.Destination).Y) / 2 : 0;
        var brick = ColorOf("Ansi1Brush");
        var flash = _shakeStart is { } flashStart && now - flashStart < 500 ? 1 - (now - flashStart) / 500.0 : 0;

        context.DrawRectangle(Brush("TerminalBackgroundBrush"), null, wall.Inflate(2), 4, 4);
        using (context.PushClip(wall))
        {
            DrawBricks(context, wall, brick, door, doorY);
            if (flash > 0)
            {
                context.DrawRectangle(new SolidColorBrush(ColorOf("StopRedBrush"), 0.4 * flash), null, wall);
            }

            if (_inspectStart is { } inspect)
            {
                var y = wall.Y + (now - inspect) % 900 / 900.0 * wall.Height;
                var accent = ColorOf("AccentBrush");
                context.DrawRectangle(new SolidColorBrush(accent, 0.2), null, new Rect(wall.X, y - 10, wall.Width, 20));
                context.DrawLine(new Pen(new SolidColorBrush(accent), 2), new Point(wall.X, y), new Point(wall.Right, y));
            }
        }

        var highlighted = _inspectStart is not null;
        context.DrawRectangle(null, new Pen(Brush(highlighted ? "AccentBrush" : "BorderBrush"), highlighted ? 2 : 1),
            wall.Inflate(2), 4, 4);

        DrawCentered(context, "NetWall", 13, Brush("TextPrimaryBrush"), wall.Center.X, wall.Bottom + 8, 160, bold: true);
        DrawCentered(context, $"política: {_result!.DefaultPolicy}", 10, Brush("TextMutedBrush"), wall.Center.X,
            wall.Bottom + 25, 160);
        if (_decision is not null)
        {
            DrawCentered(context, $"{(_allowed ? "✓" : "✕")} {_decision}", 11,
                Brush(_allowed ? "RunGreenBrush" : "StopRedBrush"), wall.Center.X, wall.Bottom + 40, 220, bold: true);
        }
    }

    internal static void DrawBricks(DrawingContext context, Rect wall, Color brick, double door, double doorY)
    {
        var rows = (int)Math.Ceiling(wall.Height / (BrickHeight + Mortar));
        for (var row = 0; row < rows; row++)
        {
            var y = wall.Y + row * (BrickHeight + Mortar);
            var offset = row % 2 == 0 ? 0 : -BrickWidth / 2;
            var inDoor = door > 0 && Math.Abs(y + BrickHeight / 2 - doorY) < 20;
            for (var x = wall.X + offset; x < wall.Right; x += BrickWidth + Mortar)
            {
                var column = (int)Math.Round((x - wall.X) / (BrickWidth + Mortar)) + 2;
                var shade = 0.6 + (row * 7 + column * 13) % 5 * 0.08;
                var slide = inDoor ? (x + BrickWidth / 2 < wall.Center.X ? -1 : 1) * door * 18 : 0;
                var rect = new Rect(x + slide, y, BrickWidth, BrickHeight);
                var alpha = inDoor ? shade * (1 - door * 0.85) : shade;
                context.DrawRectangle(new SolidColorBrush(brick, alpha), null, rect, 2, 2);
                context.DrawLine(new Pen(new SolidColorBrush(Colors.White, 0.14 * alpha), 1),
                    rect.TopLeft + new Vector(2, 1.5), rect.TopRight + new Vector(-2, 1.5));
            }
        }
    }

    private double DoorAmount(long now)
    {
        if (_doorOpened is not { } opened)
        {
            return 0;
        }

        var open = Math.Min(1, (now - opened) / 220.0);
        if (_doorClosed is not { } closed)
        {
            return open;
        }

        var close = Math.Min(1, (now - closed) / 300.0);
        if (close >= 1)
        {
            _doorOpened = null;
            _doorClosed = null;
            return 0;
        }

        return open * (1 - close);
    }

    private void DrawPacket(DrawingContext context, long now)
    {
        if (_motion is not var (path, start, duration))
        {
            return;
        }

        var points = path();
        var progress = Math.Clamp((now - start) / duration, 0, 1);
        var color = ColorOf(_packetBrush);
        for (var i = TrailLength; i >= 1; i--)
        {
            var behind = progress - i * 0.025;
            if (behind <= 0 || progress >= 1)
            {
                continue;
            }

            var size = 2.5 + (TrailLength - i) * 0.6;
            context.DrawEllipse(new SolidColorBrush(color, 0.08 + 0.3 * (TrailLength - i + 1) / TrailLength), null,
                PointAt(points, Ease(behind)), size, size);
        }

        var point = PointAt(points, Ease(progress));
        var body = new Rect(point.X - 12, point.Y - 8, 24, 16);
        context.DrawRectangle(new SolidColorBrush(color, 0.25), null, body.Inflate(3), 5, 5);
        context.DrawRectangle(new SolidColorBrush(color), null, body, 3, 3);
        var flap = new StreamGeometry();
        using (var geometry = flap.Open())
        {
            geometry.BeginFigure(body.TopLeft + new Vector(1, 1), false);
            geometry.LineTo(new Point(point.X, point.Y + 1));
            geometry.LineTo(body.TopRight + new Vector(-1, 1));
            geometry.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Brush("TerminalBackgroundBrush"), 1.5), flap);
    }

    private void DrawPulses(DrawingContext context, long now)
    {
        foreach (var (center, brush, start) in _pulses)
        {
            var k = Math.Clamp((now - start) / 700.0, 0, 1);
            var radius = 8 + k * 30;
            context.DrawEllipse(null, new Pen(new SolidColorBrush(ColorOf(brush), 1 - k), 2.5), center(), radius, radius);
        }
    }

    private void DrawHelpy(DrawingContext context, long now)
    {
        var (row, frames, milliseconds) = HelpyAnimation;
        var frame = (int)(now / milliseconds % frames);
        var hop = _helpyHopping ? -Math.Abs(Math.Sin(now / 110.0)) * 7 : 0;
        var target = new Rect(_wall.Center.X - HelpySize / 2, _wall.Top - HelpySize + 14 + hop, HelpySize, HelpySize);
        context.DrawImage(_sheet, new Rect(frame * SpriteCell, row * SpriteCell, SpriteCell, SpriteCell), target);

        if (_bubble.Length == 0)
        {
            return;
        }

        var full = Format(_bubble, 11, Brush("TextPrimaryBrush"), bold: false);
        full.MaxTextWidth = 250;
        var width = full.Width + 22;
        var height = full.Height + 14;
        var x = Math.Clamp(_wall.Center.X - width / 2, Inset, Math.Max(Inset, _design.Width - Inset - width));
        var bubble = new Rect(x, _wall.Top - HelpySize - height + 18, width, height);
        var border = _bubbleTone switch
        {
            BubbleTone.Allowed => "RunGreenBrush",
            BubbleTone.Blocked => "StopRedBrush",
            _ => "AccentBrush",
        };
        var pen = new Pen(Brush(border), 1.5);
        var tipLeft = new Point(_wall.Center.X - 7, bubble.Bottom);
        var tip = new Point(_wall.Center.X, bubble.Bottom + 7);
        var tipRight = new Point(_wall.Center.X + 7, bubble.Bottom);
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), pen, bubble, 10, 10);
        var tail = new StreamGeometry();
        using (var geometry = tail.Open())
        {
            geometry.BeginFigure(tipLeft + new Vector(0, -1), true);
            geometry.LineTo(tip);
            geometry.LineTo(tipRight + new Vector(0, -1));
            geometry.EndFigure(true);
        }

        context.DrawGeometry(Brush("SurfaceBackgroundBrush"), null, tail);
        context.DrawLine(pen, tipLeft, tip);
        context.DrawLine(pen, tip, tipRight);

        var visible = Math.Clamp((int)((now - _bubbleStart) / 16), 0, _bubble.Length);
        var text = Format(_bubble[..visible], 11, Brush("TextPrimaryBrush"), bold: false);
        text.MaxTextWidth = 250;
        context.DrawText(text, new Point(bubble.X + 11, bubble.Y + 7));
    }

    private void DrawTooltip(DrawingContext context)
    {
        if (_hover is null)
        {
            return;
        }

        var lines = new List<(string Text, bool Bold)>();
        if (_hover == WallKey)
        {
            lines.Add(("NetWall", true));
            lines.Add(($"{_result!.Rules?.Count ?? 0} reglas", false));
            lines.Add(($"política por defecto: {_result.DefaultPolicy}", false));
        }
        else if (_devices.TryGetValue(_hover, out var device))
        {
            var (sent, received, blocked) = _stats.GetValueOrDefault(device.Address);
            lines.Add((device.Name ?? device.Address, true));
            lines.Add(($"dirección {device.Address}", false));
            lines.Add((device.Host.Zone is { } zone ? $"zona {zone.Name} ({zone.Cidr})" : "fuera de las zonas declaradas",
                false));
            lines.Add(($"envía {sent} · recibe {received} · bloqueados {blocked}", false));
        }
        else
        {
            return;
        }

        var formatted = lines.Select(line =>
            Format(line.Text, 11, Brush(line.Bold ? "TextPrimaryBrush" : "TextMutedBrush"), line.Bold)).ToList();
        var width = formatted.Max(text => text.Width) + 20;
        var height = formatted.Sum(text => text.Height + 2) + 14;
        var x = _hoverPoint.X + 14 + width > _design.Width ? _hoverPoint.X - 14 - width : _hoverPoint.X + 14;
        var y = Math.Clamp(_hoverPoint.Y + 12, 4, Math.Max(4, _design.Height - height - 4));
        var card = new Rect(Math.Max(4, x), y, width, height);
        context.DrawRectangle(Brush("SurfaceBackgroundBrush"), new Pen(Brush("AccentBrush"), 1), card, 8, 8);
        var top = card.Y + 7;
        foreach (var text in formatted)
        {
            context.DrawText(text, new Point(card.X + 10, top));
            top += text.Height + 2;
        }
    }

    private static void DrawCentered(DrawingContext context, string text, double size, IBrush brush, double centerX,
        double top, double maxWidth, bool bold = false)
    {
        var formatted = Fit(text, size, brush, maxWidth, bold);
        context.DrawText(formatted, new Point(centerX - formatted.Width / 2, top));
    }

    private static FormattedText Fit(string text, double size, IBrush brush, double maxWidth, bool bold)
    {
        var formatted = Format(text, size, brush, bold);
        while (formatted.Width > maxWidth && text.Length > 2)
        {
            text = text[..^2] + "…";
            formatted = Format(text, size, brush, bold);
        }

        return formatted;
    }

    private static FormattedText Format(string text, double size, IBrush brush, bool bold) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Font, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), size, brush);

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var resource) && resource is IBrush brush ? brush : Brushes.Gray;

    private Color ColorOf(string key) => Brush(key) is ISolidColorBrush solid ? solid.Color : Colors.Gray;

    private static double Ease(double k) => k < 0.5 ? 2 * k * k : 1 - Math.Pow(-2 * k + 2, 2) / 2;

    private static double Length(IReadOnlyList<Point> path) => path.Zip(path.Skip(1), Distance).Sum();

    private static Point PointAt(IReadOnlyList<Point> path, double progress)
    {
        var distance = progress * Length(path);
        for (var i = 0; i < path.Count - 1; i++)
        {
            var segment = Distance(path[i], path[i + 1]);
            if (distance <= segment)
            {
                return path[i] + (path[i + 1] - path[i]) * (segment == 0 ? 1 : distance / segment);
            }

            distance -= segment;
        }

        return path[^1];
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private sealed record Host(string Address, SimulationNetwork? Zone, int ColorIndex);

    private sealed record Device(Host Host, string? Name, Rect Cell, Rect Icon, DeviceKind Kind, Point WallPoint,
        string Key, BuilderDevice? Model = null)
    {
        public string Address => Host.Address;
    }
}
