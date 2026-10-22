using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NetWall.IDE.Controls;
using NetWall.IDE.Models;
using NetWall.IDE.Themes;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Views;

public partial class ChallengePanel : UserControl
{
    private readonly NetWallSyntaxColorizer _colorizer = new() { IsEnabled = true };
    private ChallengeViewModel? _viewModel;
    private CancellationTokenSource? _typing;
    private bool _replacing;

    public ChallengePanel()
    {
        InitializeComponent();
        Editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        Editor.TextChanged += OnEditorTextChanged;
        Board.LevelClicked += level => _viewModel?.OpenLevel(level);
        AddHandler(KeyDownEvent, OnPanelKeyDown, RoutingStrategies.Tunnel);
    }

    public void SetHelpyColor(PetColor color)
    {
        Helpy.SetColor(color);
        Board.SetHelpyColor(color);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ThemeManager.ThemeApplied += OnThemeApplied;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
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
            _viewModel.Presenter = null;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ChallengeViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CodeReplaced += OnCodeReplaced;
            _viewModel.HelpySpoke += OnHelpySpoke;
            _viewModel.Presenter = PresentAsync;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        Board.Levels = _viewModel?.Levels ?? [];
    }

    private void OnThemeApplied(object? sender, EventArgs e) => Editor.TextArea.TextView.Redraw();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChallengeViewModel.IsPredict) && _viewModel is not null)
        {
            Editor.IsReadOnly = _viewModel.IsPredict;
        }
    }

    private void OnCodeReplaced(string code)
    {
        _replacing = true;
        Editor.Text = code;
        _replacing = false;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (!_replacing)
        {
            _viewModel?.UpdateCode(Editor.Text);
        }
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control) && _viewModel is not null)
        {
            _viewModel.CheckCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async Task PresentAsync(IReadOnlyList<ChallengeOutcome> outcomes)
    {
        CheckOverlay.IsVisible = true;
        await Task.Delay(30);
        CheckOverlay.Classes.Add("shown");
        await Stage.PlayAsync(outcomes);
        CheckOverlay.Classes.Remove("shown");
        await Task.Delay(260);
        CheckOverlay.IsVisible = false;
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
