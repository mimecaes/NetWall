using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private bool _playing;
    private int _speedIndex;

    public SimulationPanel()
    {
        InitializeComponent();
        Canvas.DeviceClicked += OnDeviceClicked;
        Canvas.DeviceConnected += device => _ = Canvas.SayAsync($"¡{device.Address} quedó conectado a NetWall!");
    }

    private double Speed => Speeds[_speedIndex];

    public void SetHelpyColor(PetColor color) => Canvas.SetHelpyColor(color);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.Loaded -= OnLoaded;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
            _viewModel.Steps.CollectionChanged -= OnStepsChanged;
        }

        _viewModel = DataContext as SimulationViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Loaded += OnLoaded;
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

        var token = _cancel.Token;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Canvas.SayAsync(_viewModel.HelpyGreeting);
        if (!token.IsCancellationRequested)
        {
            await PlayAsync();
        }
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

    private void OnDeviceClicked(BuilderDevice device)
    {
        if (Resources["DeviceFlyout"] is not Flyout flyout || flyout.Content is not Control editor)
        {
            return;
        }

        editor.DataContext = device;
        flyout.ShowAt(Canvas, showAtPointer: true);
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
            (Resources["DeviceFlyout"] as Flyout)?.Hide();
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
        if (_playing)
        {
            _playing = false;
            UpdatePlayIcon();
            return;
        }

        if (_viewModel is { NextPacket: null, Result: not null })
        {
            Stop();
            _viewModel.Reset();
        }

        await PlayAsync();
    }

    private async void OnStepClick(object? sender, RoutedEventArgs e) => await StepAsync();

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        Stop();
        _viewModel?.Reset();
    }

    private void Stop()
    {
        _cancel.Cancel();
        _cancel = new CancellationTokenSource();
        _playing = false;
        UpdatePlayIcon();
        Canvas.Clear();
    }

    private async Task PlayAsync()
    {
        if (_viewModel?.NextPacket is null)
        {
            return;
        }

        _playing = true;
        UpdatePlayIcon();
        var token = _cancel.Token;
        while (_playing && await StepAsync())
        {
            try
            {
                await Task.Delay((int)(400 / Speed), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        _playing = false;
        UpdatePlayIcon();
        if (_viewModel.NextPacket is null)
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
        await viewModel.EvaluateAsync(packet, (int)(380 / Speed), token);
        Canvas.EndInspect();

        Canvas.ShowDecision(packet);
        viewModel.MarkDecisive(packet);
        var verdict = Canvas.SayAsync(viewModel.HelpyVerdict(packet),
            packet.IsAllowed ? BubbleTone.Allowed : BubbleTone.Blocked);
        if (packet.IsAllowed)
        {
            await Task.Delay((int)(220 / Speed), token);
            await Canvas.TravelAsync(() => [Canvas.PathToWall(packet)[^1], .. Canvas.PathFromWall(packet)], Speed,
                token);
        }
        else
        {
            await Canvas.TravelAsync(() => Canvas.BouncePath(packet), Speed * 1.6, token);
        }

        Canvas.Arrive(packet);
        await verdict.WaitAsync(token);
        await Task.Delay((int)(600 / Speed), token);
        Canvas.HidePacket();
    }

    private void UpdatePlayIcon()
    {
        PlayIcon.Kind = _playing ? LucideIconKind.Pause : LucideIconKind.Play;
        ToolTip.SetTip(PlayButton, _playing ? "Pausar" : "Reproducir");
    }
}
