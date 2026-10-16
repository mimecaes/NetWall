using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NetWall.IDE.Controls;
using NetWall.IDE.Models;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Views.Dialogs;

public partial class CodeAssistantDialog : Window
{
    private const double CardWidth = 432;

    private CodeAssistantViewModel? _viewModel;
    private CancellationTokenSource? _typing;
    private bool _expanded;
    private bool _closing;
    private int _viewVersion;

    public CodeAssistantDialog()
    {
        InitializeComponent();
        DataContext = new CodeAssistantViewModel();
        CardContent.SizeChanged += (_, _) => FitCard();
        Header.PointerPressed += (_, e) => BeginMoveDrag(e);
        KeyDown += OnKeyDown;
    }

    public static Task<string?> ShowAsync(Window owner, PetColor helpyColor, string? currentFile, string? projectRoot,
        string documentText)
    {
        var dialog = new CodeAssistantDialog
        {
            DataContext = new CodeAssistantViewModel(currentFile, projectRoot, documentText),
        };
        dialog.Helpy.SetColor(helpyColor);
        return dialog.ShowDialog<string?>(owner);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as CodeAssistantViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await PlayIntroAsync();
    }

    private async Task PlayIntroAsync()
    {
        await Task.Delay(120);
        Avatar.Classes.Add("shown");
        Helpy.Mood = HelpyMood.Happy;

        await Task.Delay(450);
        Card.Width = CardWidth;
        Card.CornerRadius = new CornerRadius(16);

        await Task.Delay(400);
        Bubble.Classes.Add("shown");
        await TypeAsync(_viewModel?.HelpyMessage ?? string.Empty, 32);

        Helpy.Classes.Add("floating");
        _expanded = true;
        MenuView.IsVisible = true;
        await RevealRowsAsync(MenuView, _viewVersion);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CodeAssistantViewModel.HelpyMessage) && _expanded && _viewModel is not null)
        {
            _ = TypeAsync(_viewModel.HelpyMessage, 14);
        }
        else if (e.PropertyName == nameof(CodeAssistantViewModel.Kind) && _viewModel is not null)
        {
            _ = SwitchViewAsync(toForm: _viewModel.Kind != AssistantKind.None);
        }
    }

    private async Task TypeAsync(string text, int millisecondsPerLetter)
    {
        _typing?.Cancel();
        var typing = new CancellationTokenSource();
        _typing = typing;

        Helpy.Mood = HelpyMood.Talking;
        for (var i = 1; i <= text.Length; i++)
        {
            if (typing.IsCancellationRequested)
            {
                return;
            }

            BubbleText.Text = text[..i];
            await Task.Delay(millisecondsPerLetter);
        }

        if (!typing.IsCancellationRequested)
        {
            Helpy.Mood = _viewModel?.HelpyIsHappy == true ? HelpyMood.Happy : HelpyMood.Idle;
        }
    }

    private async Task SwitchViewAsync(bool toForm)
    {
        var version = ++_viewVersion;
        var from = toForm ? MenuView : FormView;
        var to = toForm ? FormView : MenuView;

        from.Classes.Add("leaving");
        await Task.Delay(180);
        if (version != _viewVersion)
        {
            return;
        }

        from.IsVisible = false;
        from.Classes.Remove("leaving");
        to.IsVisible = true;
        await RevealRowsAsync(to, version);
    }

    private async Task RevealRowsAsync(Control view, int version)
    {
        view.UpdateLayout();
        var rows = view.GetVisualDescendants().OfType<Control>()
            .Where(control => control.Classes.Contains("helpy-row") && IsShownWithin(control, view))
            .ToList();

        foreach (var row in rows)
        {
            row.Classes.Remove("shown");
            row.IsVisible = false;
        }

        foreach (var row in rows)
        {
            if (version != _viewVersion)
            {
                return;
            }

            row.IsVisible = true;
            await Task.Delay(16);
            row.Classes.Add("shown");
            await Task.Delay(70);
        }
    }

    private static bool IsShownWithin(Control control, Control view)
    {
        for (var parent = control.GetVisualParent() as Control; parent is not null && parent != view;
             parent = parent.GetVisualParent() as Control)
        {
            if (!parent.IsVisible)
            {
                return false;
            }
        }

        return true;
    }

    private void FitCard()
    {
        if (_expanded)
        {
            Card.Height = CardContent.Bounds.Height + Card.BorderThickness.Top + Card.BorderThickness.Bottom;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _ = CloseAnimatedAsync(null);
        }
        else if (e.Key == Key.Enter && _viewModel is { CanInsert: true })
        {
            OnInsertClick(sender, e);
        }
    }

    private async void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        if (_closing || _viewModel is not { CanInsert: true } viewModel)
        {
            return;
        }

        _ = TypeAsync("¡Listo!", 20);
        Helpy.Mood = HelpyMood.Happy;
        await Task.Delay(350);
        await CloseAnimatedAsync(viewModel.GenerateCode());
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _ = CloseAnimatedAsync(null);

    private async Task CloseAnimatedAsync(string? result)
    {
        if (_closing && result is null)
        {
            return;
        }

        _closing = true;
        Card.Classes.Add("closing");
        await Task.Delay(220);
        Close(result);
    }
}
