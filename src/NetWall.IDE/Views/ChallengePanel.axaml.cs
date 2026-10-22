using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NetWall.IDE.Controls;
using NetWall.IDE.Models;
using NetWall.IDE.Themes;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Views;

public partial class ChallengePanel : UserControl
{
    private readonly NetWallSyntaxColorizer _colorizer = new() { IsEnabled = true };
    private readonly RuleHighlighter _highlighter = new();
    private ChallengeViewModel? _viewModel;
    private CancellationTokenSource? _typing;
    private bool _replacing;
    private bool _turning;
    private bool _correcting;
    private double _pace = 1;

    public ChallengePanel()
    {
        InitializeComponent();
        Editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_highlighter);
        AddHandler(PointerPressedEvent, OnPanelPointerPressed, RoutingStrategies.Tunnel);
        Editor.TextChanged += OnEditorTextChanged;
        AddHandler(KeyDownEvent, OnPanelKeyDown, RoutingStrategies.Tunnel);
    }

    public void SetHelpyColor(PetColor color)
    {
        Helpy.SetColor(color);
        Correction.SetHelpyColor(color);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ThemeManager.ThemeApplied += OnThemeApplied;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ThemeManager.ThemeApplied -= OnThemeApplied;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.CodeReplaced -= OnCodeReplaced;
            _viewModel.HelpySpoke -= OnHelpySpoke;
            _viewModel.SealAwarded -= OnSealAwarded;
            _viewModel.Presenter = null;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ChallengeViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CodeReplaced += OnCodeReplaced;
            _viewModel.HelpySpoke += OnHelpySpoke;
            _viewModel.SealAwarded += OnSealAwarded;
            _viewModel.Presenter = PresentAsync;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnThemeApplied(object? sender, EventArgs e) => Editor.TextArea.TextView.Redraw();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChallengeViewModel.IsPredict) && _viewModel is not null)
        {
            Editor.IsReadOnly = _viewModel.IsPredict;
        }
    }

    private void OnSealAwarded(int seal) => Seal.Drop();

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => _ = TurnAsync(false);

    private void OnNextClick(object? sender, RoutedEventArgs e) => _ = TurnAsync(true);

    private void OnPreviousCornerPressed(object? sender, PointerPressedEventArgs e) => _ = TurnAsync(false);

    private void OnNextCornerPressed(object? sender, PointerPressedEventArgs e) => _ = TurnAsync(true);

    private async Task TurnAsync(bool forward)
    {
        if (_turning || _viewModel is null || !(forward ? _viewModel.CanGoForward : _viewModel.CanGoBack))
        {
            return;
        }

        _turning = true;
        PageTurn.Cover(Snapshot(), PageRect(LeftPage), PageRect(RightPage));
        if (forward)
        {
            _viewModel.GoForward();
        }
        else
        {
            _viewModel.GoBack();
        }

        await Task.Delay(60);
        await PageTurn.PlayAsync(Snapshot(), forward);
        _turning = false;
    }

    private RenderTargetBitmap Snapshot()
    {
        var size = Notebook.Bounds.Size;
        var bitmap = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)size.Width), Math.Max(1, (int)size.Height)));
        bitmap.Render(Notebook);
        return bitmap;
    }

    private Rect PageRect(Control page) =>
        new(page.TranslatePoint(default, Notebook) ?? default, page.Bounds.Size);

    private void OnCodeReplaced(string code)
    {
        ClearCorrection();
        _replacing = true;
        Editor.Text = code;
        _replacing = false;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (!_replacing)
        {
            ClearCorrection();
            _viewModel?.UpdateCode(Editor.Text);
        }
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _viewModel.CheckCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.None && !Editor.TextArea.IsKeyboardFocusWithin
                 && e.Key is Key.Left or Key.Right)
        {
            _ = TurnAsync(e.Key == Key.Right);

            e.Handled = true;
        }
    }

    private async Task PresentAsync(IReadOnlyList<ChallengeOutcome> outcomes)
    {
        ClearCorrection();
        _correcting = true;
        _pace = outcomes.Count switch
        {
            <= 3 => 1,
            <= 5 => 0.75,
            _ => 0.55,
        };
        var chips = Enumerable.Range(0, outcomes.Count).Select(ChipRect).ToList();
        if (chips.Count > 0)
        {
            Correction.ShowHelpy(new Point(chips[0].Left - 26, chips[0].Top + 4));
        }

        for (var i = 0; i < outcomes.Count; i++)
        {
            await Correction.WalkToAsync(new Point(chips[i].Center.X, chips[i].Top + 4), 340 * _pace);
            if (outcomes[i] is { Allowed: { } allowed, Test.Visible: true } outcome)
            {
                await HighlightAsync(outcome.Rule, allowed);
            }

            await Correction.MarkAsync(chips[i], outcomes[i].Passed, 420 * _pace);
            await Task.Delay((int)(140 * _pace));
        }

        var success = outcomes.All(outcome => outcome.Passed);
        var anchor = new Point(TestList.Bounds.Width, -6);
        anchor = TestList.TranslatePoint(anchor, Correction) ?? anchor;
        await Correction.NoteAsync(success ? "¡Muy bien!" : "Revisa esto", success, anchor, 650 * Math.Max(_pace, 0.6));
        await Task.Delay((int)(350 * _pace));
        await Correction.HideHelpyAsync();
        _correcting = false;
    }

    private Rect ChipRect(int index)
    {
        if (TestList.ContainerFromIndex(index) is not { } chip)
        {
            return default;
        }

        var origin = chip.TranslatePoint(default, Correction) ?? default;
        return new Rect(origin, chip.Bounds.Size);
    }

    private async Task HighlightAsync(string? rule, bool allowed)
    {
        if (RuleLines(rule) is not var (first, last))
        {
            return;
        }

        var color = this.TryFindResource(allowed ? "PastelMintBrush" : "PastelPinkBrush", out var resource)
                    && resource is ISolidColorBrush brush ? brush.Color : Colors.Plum;
        _highlighter.Show(first, last, color, rule is null ? "← política" : "← decide");
        Editor.ScrollToLine(first);
        var duration = 480 * _pace;
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < duration)
        {
            _highlighter.Progress = clock.ElapsedMilliseconds / duration;
            Editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
            await Task.Delay(16);
        }

        _highlighter.Progress = 1;
        Editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
        await Task.Delay((int)(380 * _pace));
    }

    private (int First, int Last)? RuleLines(string? rule)
    {
        var lines = Editor.Text.Split('\n');
        var pattern = rule is null ? @"^\s*politica_defecto\b" : $@"^\s*regla\s+{Regex.Escape(rule)}\b";
        var first = Array.FindIndex(lines, line => Regex.IsMatch(line, pattern));
        if (first < 0)
        {
            return null;
        }

        var last = rule is null ? first : Array.FindIndex(lines, first, line => line.Contains("};"));
        return (first + 1, (last < 0 ? first : last) + 1);
    }

    private void ClearCorrection()
    {
        if (_correcting)
        {
            return;
        }

        Correction.Clear();
        if (_highlighter.IsActive)
        {
            _highlighter.Clear();
            Editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
        }
    }

    private void OnPanelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_correcting)
        {
            _pace = 0.15;
        }
    }

    private void OnHelpySpoke(string text, BubbleTone tone) => _ = TypeAsync(text, tone);

    private async Task TypeAsync(string text, BubbleTone tone)
    {
        _typing?.Cancel();
        var typing = new CancellationTokenSource();
        _typing = typing;
        Bubble.Classes.Set("ok", tone == BubbleTone.Allowed);
        Bubble.Classes.Set("no", tone == BubbleTone.Blocked);
        Helpy.Mood = HelpyMood.Talking;
        for (var i = 1; i <= text.Length; i++)
        {
            if (typing.IsCancellationRequested)
            {
                return;
            }

            BubbleText.Text = text[..i];
            await Task.Delay(14);
        }

        Helpy.Mood = tone == BubbleTone.Allowed ? HelpyMood.Happy : HelpyMood.Idle;
    }
}
