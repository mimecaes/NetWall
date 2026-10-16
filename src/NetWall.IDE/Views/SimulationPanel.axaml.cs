using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Lucide.Avalonia;
using NetWall.IDE.Controls;
using NetWall.IDE.Models;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Views;

public partial class SimulationPanel : UserControl
{
    private static readonly double[] Speeds = [1, 2, 0.5];

    private SimulationViewModel? _viewModel;
    private CancellationTokenSource _cancel = new();
    private bool _looping;
    private bool _running;
    private int _runId;
    private BuilderDevice? _editing;
    private CancellationTokenSource? _editorTyping;
    private PetColor _helpyColor = PetColor.Gray;
    private Point _pressPoint;
    private Point _editorAnchor;
    private Point? _editorGrab;
    private int _speedIndex;

    public SimulationPanel()
    {
        InitializeComponent();
        Canvas.DeviceClicked += OnDeviceClicked;
        Canvas.DeviceConnected += device => _ = Canvas.SayAsync($"¡{device.Address} quedó conectado a NetWall!");
        Canvas.PacketDragged += (source, destination) => ShowPacketForm(source, destination);
        AddHandler(PointerPressedEvent, OnPanelPointerPressed, RoutingStrategies.Tunnel);
    }

    private double Speed => Speeds[_speedIndex];

    public void SetHelpyColor(PetColor color)
    {
        _helpyColor = color;
        Canvas.SetHelpyColor(color);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.Loaded -= OnLoaded;
            _viewModel.PacketDelivered -= OnPacketDelivered;
            _viewModel.Replayed -= OnReplayed;
            _viewModel.RulesChanged -= OnRulesChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
            _viewModel.Steps.CollectionChanged -= OnStepsChanged;
        }

        _viewModel = DataContext as SimulationViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Loaded += OnLoaded;
            _viewModel.PacketDelivered += OnPacketDelivered;
            _viewModel.Replayed += OnReplayed;
            _viewModel.RulesChanged += OnRulesChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Entries.CollectionChanged += OnEntriesChanged;
            _viewModel.Steps.CollectionChanged += OnStepsChanged;
        }
    }

    private async void OnLoaded()
    {
        Stop();
        Canvas.Result = _viewModel?.Result;
        Canvas.Builder = _viewModel is { IsBuilder: true } ? _viewModel.Devices : null;
        if (_viewModel?.Result is null)
        {
            return;
        }

        if (_viewModel.IsBuilder)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Canvas.SayAsync(_viewModel.HelpyGreeting);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Canvas.SayAsync(_viewModel.HelpyGreeting);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SimulationViewModel.SelectedEntry)
            && _viewModel is { IsAnimating: false, SelectedEntry: { } entry })
        {
            Canvas.ShowRoute(entry.Packet);
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            Dispatcher.UIThread.Post(LogScroll.ScrollToEnd, DispatcherPriority.Background);
        }
    }

    private void OnEntryTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: SimulationEntry entry } && _viewModel is { IsAnimating: false } viewModel)
        {
            viewModel.SelectedEntry = entry;
        }
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (RuleStep step in e.NewItems ?? Array.Empty<RuleStep>())
        {
            step.PropertyChanged += OnStepPropertyChanged;
        }
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is RuleStep { IsEvaluating: true } step)
        {
            StepList.ContainerFromItem(step)?.BringIntoView();
        }
    }

    private void OnHelpyClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.IsHelpyEnabled = !_viewModel.IsHelpyEnabled;
        Canvas.HelpyEnabled = _viewModel.IsHelpyEnabled;
    }

    private void OnPaletteItemPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: string tag } || !Enum.TryParse<DeviceKind>(tag, out var kind)
            || _viewModel is null)
        {
            return;
        }

        var position = Canvas.Normalize(e.GetPosition(Canvas));
        Canvas.BeginCarry(_viewModel.AddDevice(kind, position.X, position.Y), e);
        e.Handled = true;
    }

    private void OnPanelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressPoint = e.GetPosition(EditorLayer);
        if (DeviceEditor.IsVisible && e.Source is Visual source && !DeviceEditor.IsVisualAncestorOf(source)
            && source != DeviceEditor)
        {
            HideEditor();
        }
    }

    private async void OnDeviceClicked(BuilderDevice device)
    {
        StopEditing();
        _editing = device;
        device.PropertyChanged += OnEditingChanged;
        DeviceEditor.DataContext = device;
        EditorHelpy.SetColor(_helpyColor);
        var rows = EditorRows.Children;
        DeviceEditor.Classes.Remove("shown");
        foreach (var row in rows)
        {
            row.Classes.Remove("shown");
        }

        DeviceEditor.IsVisible = true;
        DeviceEditor.Measure(Size.Infinity);
        var size = DeviceEditor.DesiredSize;
        var left = _pressPoint.X + 24 + size.Width > EditorLayer.Bounds.Width
            ? _pressPoint.X - 24 - size.Width
            : _pressPoint.X + 24;
        _editorAnchor = Clamp(new Point(left, _pressPoint.Y - 60));
        MoveEditor(_editorAnchor);
        await Task.Delay(30);
        DeviceEditor.Classes.Add("shown");
        _ = TypeEditorAsync(device.Question, ok: null);
        foreach (var row in rows)
        {
            row.Classes.Add("shown");
            await Task.Delay(45);
        }
    }

    private void OnEditingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuilderDevice.HelpyMessage) && sender is BuilderDevice device)
        {
            _ = TypeEditorAsync(device.HelpyMessage, ok: !device.HasError);
        }
    }

    private async void HideEditor()
    {
        StopEditing();
        DeviceEditor.Classes.Remove("shown");
        await Task.Delay(200);
        if (_editing is null)
        {
            DeviceEditor.IsVisible = false;
        }
    }

    private void OnEditorHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(EditorHeader).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _editorGrab = e.GetPosition(EditorLayer) - new Point(Avalonia.Controls.Canvas.GetLeft(DeviceEditor),
            Avalonia.Controls.Canvas.GetTop(DeviceEditor));
        EditorHeader.Classes.Add("dragging");
        e.Pointer.Capture(EditorHeader);
        e.Handled = true;
    }

    private void OnEditorHeaderMoved(object? sender, PointerEventArgs e)
    {
        if (_editorGrab is { } grab)
        {
            MoveEditor(Clamp(e.GetPosition(EditorLayer) - grab));
        }
    }

    private void OnEditorHeaderReleased(object? sender, PointerReleasedEventArgs e)
    {
        _editorGrab = null;
        EditorHeader.Classes.Remove("dragging");
        e.Pointer.Capture(null);
    }

    private async void OnEditorHeaderDoubleTapped(object? sender, TappedEventArgs e)
    {
        var from = new Point(Avalonia.Controls.Canvas.GetLeft(DeviceEditor), Avalonia.Controls.Canvas.GetTop(DeviceEditor));
        for (var step = 1; step <= 12; step++)
        {
            var k = 1 - Math.Pow(1 - step / 12.0, 3);
            MoveEditor(from + (_editorAnchor - from) * k);
            await Task.Delay(16);
        }
    }

    private void MoveEditor(Point position)
    {
        Avalonia.Controls.Canvas.SetLeft(DeviceEditor, position.X);
        Avalonia.Controls.Canvas.SetTop(DeviceEditor, position.Y);
    }

    private Point Clamp(Point position)
    {
        var size = DeviceEditor.Bounds.Size == default ? DeviceEditor.DesiredSize : DeviceEditor.Bounds.Size;
        return new Point(
            Math.Clamp(position.X, 8, Math.Max(8, EditorLayer.Bounds.Width - size.Width - 8)),
            Math.Clamp(position.Y, 8, Math.Max(8, EditorLayer.Bounds.Height - size.Height - 8)));
    }

    private void StopEditing()
    {
        _editorTyping?.Cancel();
        if (_editing is not null)
        {
            _editing.PropertyChanged -= OnEditingChanged;
            _editing = null;
        }
    }

    private async Task TypeEditorAsync(string text, bool? ok)
    {
        _editorTyping?.Cancel();
        var typing = new CancellationTokenSource();
        _editorTyping = typing;
        EditorBubble.Classes.Set("ok", ok == true);
        EditorBubble.Classes.Set("no", ok == false);
        EditorHelpy.Mood = HelpyMood.Talking;
        for (var i = 1; i <= text.Length; i++)
        {
            if (typing.IsCancellationRequested)
            {
                return;
            }

            EditorBubbleText.Text = text[..i];
            await Task.Delay(14);
        }

        EditorHelpy.Mood = ok == true ? HelpyMood.Happy : HelpyMood.Idle;
    }

    private void OnSendFromEditorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: BuilderDevice device })
        {
            HideEditor();
            ShowPacketForm(device, null);
        }
    }

    private void ShowPacketForm(BuilderDevice source, BuilderDevice? destination)
    {
        if (_viewModel is null || Resources["PacketFlyout"] is not Flyout flyout || flyout.Content is not Control form)
        {
            return;
        }

        form.DataContext = _viewModel.NewDraft(source, destination);
        flyout.ShowAt(Canvas, showAtPointer: true);
    }

    private void OnSendPacketClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PacketDraft draft } && _viewModel is not null && _viewModel.Send(draft))
        {
            (Resources["PacketFlyout"] as Flyout)?.Hide();
        }
    }

    private async void OnPacketDelivered()
    {
        while (await StepAsync())
        {
        }
    }

    private async void OnReplayed()
    {
        Stop();
        _looping = true;
        await RunAsync();
    }

    private void OnRulesChanged()
    {
        if (_viewModel is { IsBuilder: true } viewModel)
        {
            var active = viewModel.ActiveRules.Count();
            _ = Canvas.SayAsync(
                $"Los próximos paquetes usarán {active} {(active == 1 ? "regla" : "reglas")}. Con ↻ repito los enviados.");
        }
    }

    private void OnToggleConnectClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: BuilderDevice device })
        {
            device.IsConnected = !device.IsConnected;
        }
    }

    private void OnRemoveDeviceClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: BuilderDevice device })
        {
            HideEditor();
            _viewModel?.RemoveDevice(device);
        }
    }

    private void OnAllRulesClick(object? sender, RoutedEventArgs e) => _viewModel?.SetAllRules(true);

    private void OnNoRulesClick(object? sender, RoutedEventArgs e) => _viewModel?.SetAllRules(false);

    private void OnSpeedClick(object? sender, RoutedEventArgs e)
    {
        _speedIndex = (_speedIndex + 1) % Speeds.Length;
        SpeedButton.Content = $"x{Speed:0.#}";
    }

    private async void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (_running)
        {
            Canvas.Paused = !Canvas.Paused;
            _looping = true;
            UpdatePlayIcon();
            return;
        }

        if (_viewModel is { NextPacket: null, Result: not null })
        {
            Stop();
            _viewModel.Reset();
        }

        _looping = true;
        await RunAsync();
    }

    private async void OnStepClick(object? sender, RoutedEventArgs e)
    {
        _looping = false;
        if (_running)
        {
            Canvas.Paused = false;
            UpdatePlayIcon();
            return;
        }

        await RunAsync();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is { IsBuilder: true } builder)
        {
            builder.RequestReplay();
            return;
        }

        Stop();
        _viewModel?.Reset();
    }

    private void Stop()
    {
        _cancel.Cancel();
        _cancel = new CancellationTokenSource();
        _runId++;
        _running = false;
        _looping = false;
        Canvas.Paused = false;
        UpdatePlayIcon();
        Canvas.Clear();
    }

    private async Task RunAsync()
    {
        if (_running || _viewModel?.NextPacket is null)
        {
            return;
        }

        var run = ++_runId;
        var token = _cancel.Token;
        _running = true;
        UpdatePlayIcon();
        try
        {
            while (_viewModel.IsAnimating)
            {
                await Task.Delay(15, token);
            }

            while (await StepAsync() && _looping && _viewModel.NextPacket is not null)
            {
                await Canvas.WaitAsync(400 / Speed, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (run == _runId)
            {
                _running = false;
                UpdatePlayIcon();
            }
        }

        if (!token.IsCancellationRequested && _viewModel is { NextPacket: null, IsBuilder: false })
        {
            await Canvas.SayAsync(_viewModel.HelpySummary, BubbleTone.Allowed);
        }
    }

    private async Task<bool> StepAsync()
    {
        if (_viewModel is not { IsAnimating: false, NextPacket: { } packet } viewModel)
        {
            return false;
        }

        viewModel.IsAnimating = true;
        try
        {
            await AnimateAsync(viewModel, packet, _cancel.Token);
            viewModel.AddEntry(packet);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            viewModel.IsAnimating = false;
        }
    }

    private async Task AnimateAsync(SimulationViewModel viewModel, SimulatedPacket packet, CancellationToken token)
    {
        Canvas.Begin(packet);
        var arrival = Canvas.SayAsync(viewModel.HelpyArrival(packet));
        await Canvas.TravelAsync(() => Canvas.PathToWall(packet), Speed, token);
        await arrival.WaitAsync(token);

        Canvas.BeginInspect();
        await viewModel.EvaluateAsync(packet, () => Canvas.WaitAsync(380 / Speed, token));
        Canvas.EndInspect();

        Canvas.ShowDecision(packet);
        viewModel.MarkDecisive(packet);
        var verdict = Canvas.SayAsync(viewModel.HelpyVerdict(packet),
            packet.IsAllowed ? BubbleTone.Allowed : BubbleTone.Blocked);
        if (packet.IsAllowed)
        {
            await Canvas.WaitAsync(220 / Speed, token);
            await Canvas.TravelAsync(() => [Canvas.PathToWall(packet)[^1], .. Canvas.PathFromWall(packet)], Speed,
                token);
        }
        else
        {
            await Canvas.TravelAsync(() => Canvas.BouncePath(packet), Speed * 1.6, token);
        }

        Canvas.Arrive(packet);
        await verdict.WaitAsync(token);
        await Canvas.WaitAsync(600 / Speed, token);
        Canvas.HidePacket();
    }

    private void UpdatePlayIcon()
    {
        var playing = _running && !Canvas.Paused;
        PlayIcon.Kind = playing ? LucideIconKind.Pause : LucideIconKind.Play;
        ToolTip.SetTip(PlayButton, playing ? "Pausar" : Canvas.Paused ? "Continuar" : "Reproducir");
    }
}
