using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using NetWall.IDE.Controls;
using NetWall.IDE.Models;
using NetWall.IDE.Themes;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Views;

public partial class MainWindow : Window
{
    private sealed class EditorPane
    {
        public required TextEditor Editor { get; init; }
        public required Func<MainViewModel, EditorTabViewModel?> GetTab { get; init; }
        public bool SuppressTextChanged { get; set; }
    }

    private readonly IndentRainbowRenderer _indentRainbowRenderer = new();
    private DispatcherTimer? _indentFadeTimer;
    private EditorPane _primaryPane = null!;
    private EditorPane _secondaryPane = null!;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        _primaryPane = new EditorPane { Editor = CodeEditor, GetTab = vm => vm.SelectedTab };
        _secondaryPane = new EditorPane { Editor = SecondaryCodeEditor, GetTab = vm => vm.SelectedSecondaryTab };

        foreach (var pane in new[] { _primaryPane, _secondaryPane })
        {
            pane.Editor.TextChanged += (_, _) => OnEditorTextChanged(pane);
            pane.Editor.TextArea.Caret.PositionChanged += (_, _) => OnEditorCaretPositionChanged(pane);
            pane.Editor.TextArea.TextView.BackgroundRenderers.Add(_indentRainbowRenderer);
        }

        SecondaryCodeEditor.KeyDown += OnSecondaryEditorKeyDown;

        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ItemsControl>("TabsItemsControl") is { } tabsControl)
        {
            tabsControl.LayoutUpdated += OnTabsLayoutUpdated;
        }
    }

    private void OnTabsLayoutUpdated(object? sender, EventArgs e)
    {
        // This will be called when tabs are updated, allowing us to handle animations
    }

    private async void OnTabCloseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not EditorTabViewModel tab)
        {
            return;
        }

        var tabBorder = button.FindAncestorOfType<Border>();
        if (tabBorder is not null)
        {
            tabBorder.Classes.Add("closing");
            await Task.Delay(200);
        }

        ViewModel?.CloseTabCommand.Execute(tab);
    }

    private async void OnSecondaryTabCloseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not EditorTabViewModel tab)
        {
            return;
        }

        var tabBorder = button.FindAncestorOfType<Border>();
        if (tabBorder is not null)
        {
            tabBorder.Classes.Add("closing");
            await Task.Delay(200);
        }

        ViewModel?.CloseSecondaryTabCommand.Execute(tab);
    }

    private void OnSecondaryEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.S || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        if (ViewModel?.SelectedSecondaryTab is not { } tab)
        {
            return;
        }

        if (string.IsNullOrEmpty(tab.FilePath))
        {
            ViewModel.RequestSaveAsFor(tab);
        }
        else
        {
            ViewModel.SaveTab(tab);
        }

        e.Handled = true;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.RequestOpenFolder += OnRequestOpenFolder;
            vm.RequestSaveAs += OnRequestSaveAs;
            vm.RequestDeleteConfirmation += OnRequestDeleteConfirmation;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.ExtensionToggled += OnExtensionToggled;
            _indentRainbowRenderer.Opacity = vm.IsIndentRainbowEnabled ? 1.0 : 0.0;
        }
    }

    private void OnExtensionToggled(ExtensionInfo extension)
    {
        if (extension.Id == "indent-rainbow")
        {
            AnimateIndentRainbowOpacity(extension.IsEnabled);
        }
    }

    private void AnimateIndentRainbowOpacity(bool enabled)
    {
        _indentFadeTimer?.Stop();

        var start = _indentRainbowRenderer.Opacity;
        var target = enabled ? 1.0 : 0.0;
        var startedAt = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(300);

        _indentFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _indentFadeTimer.Tick += (_, _) =>
        {
            var progress = Math.Min(1.0, (DateTime.UtcNow - startedAt).TotalMilliseconds / duration.TotalMilliseconds);
            _indentRainbowRenderer.Opacity = start + (target - start) * progress;
            CodeEditor.TextArea.TextView.InvalidateVisual();
            SecondaryCodeEditor.TextArea.TextView.InvalidateVisual();

            if (progress >= 1.0)
            {
                _indentFadeTimer?.Stop();
            }
        };
        _indentFadeTimer.Start();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.TerminalOutput))
        {
            TerminalScrollViewer.ScrollToEnd();
        }
        else if (e.PropertyName == nameof(MainViewModel.SelectedTab))
        {
            PushTabContent(_primaryPane);
        }
        else if (e.PropertyName == nameof(MainViewModel.SelectedSecondaryTab))
        {
            PushTabContent(_secondaryPane);
        }
    }

    private void PushTabContent(EditorPane pane)
    {
        pane.SuppressTextChanged = true;
        pane.Editor.Text = ViewModel is { } vm ? pane.GetTab(vm)?.Content ?? string.Empty : string.Empty;
        pane.SuppressTextChanged = false;
    }

    private void OnEditorTextChanged(EditorPane pane)
    {
        if (pane.SuppressTextChanged)
        {
            return;
        }

        Pet.NotifyTyping();

        if (ViewModel is { } vm && pane.GetTab(vm) is { } tab)
        {
            tab.Content = pane.Editor.Text;
        }
    }

    private void OnEditorCaretPositionChanged(EditorPane pane)
    {
        Pet.NotifyActivity();

        if (ViewModel is { } vm && pane.GetTab(vm) is { } tab)
        {
            tab.Line = pane.Editor.TextArea.Caret.Line;
            tab.Column = pane.Editor.TextArea.Caret.Column;
        }
    }

    private async void OnRequestOpenFolder()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false,
        });

        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            ViewModel?.LoadFolder(path);
        }
    }

    private async void OnRequestSaveAs(EditorTabViewModel tab)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File",
            SuggestedFileName = tab.Title,
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            ViewModel?.SaveTab(tab, path);
        }
    }

    private Task<bool> OnRequestDeleteConfirmation(FileTreeNode node)
    {
        var message = node.IsFolder
            ? $"Delete the folder '{node.Name}' and everything inside it? This cannot be undone."
            : $"Delete '{node.Name}'? This cannot be undone.";
        return ConfirmDialog.AskAsync(this, message);
    }

    private void OnRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: FileTreeNode node })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            ViewModel?.CommitRenameCommand.Execute(node);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel?.CancelRenameCommand.Execute(node);
            e.Handled = true;
        }
    }

    private void OnRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: FileTreeNode node })
        {
            ViewModel?.CommitRenameCommand.Execute(node);
        }
    }

    private void OnTerminalInputKeyDown(object? sender, KeyEventArgs e)
    {
        Pet.NotifyActivity();

        if (e.Key == Key.Enter)
        {
            ViewModel?.RunTerminalLineCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnThemeMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tagValue } && Enum.TryParse<AppTheme>(tagValue, out var theme))
        {
            ThemeManager.Apply(theme);
        }
    }

    private void OnPetColorMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tagValue } && Enum.TryParse<PetColor>(tagValue, out var color))
        {
            Pet.SetColor(color);
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source &&
            (source.FindAncestorOfType<Button>(includeSelf: true) is not null ||
             source.FindAncestorOfType<TextBox>(includeSelf: true) is not null))
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnGlobalSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                var index = vm.GlobalSearchSelectedIndex >= 0 ? vm.GlobalSearchSelectedIndex : 0;
                if (index < vm.GlobalSearchResults.Count)
                {
                    vm.OpenGlobalSearchResultCommand.Execute(vm.GlobalSearchResults[index].Node);
                }

                e.Handled = true;
                break;

            case Key.Down:
                vm.MoveGlobalSearchSelection(1);
                e.Handled = true;
                break;

            case Key.Up:
                vm.MoveGlobalSearchSelection(-1);
                e.Handled = true;
                break;

            case Key.Escape:
                vm.GlobalSearchQuery = string.Empty;
                e.Handled = true;
                break;
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
