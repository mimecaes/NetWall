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
using NetWall.IDE.Controls;
using NetWall.IDE.Models;
using NetWall.IDE.Themes;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Views;

public partial class MainWindow : Window
{
    private bool _suppressEditorTextChanged;
    private readonly IndentRainbowRenderer _indentRainbowRenderer = new();
    private DispatcherTimer? _indentFadeTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        CodeEditor.TextChanged += OnEditorTextChanged;
        CodeEditor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_indentRainbowRenderer);
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
            _suppressEditorTextChanged = true;
            CodeEditor.Text = ViewModel?.SelectedTab?.Content ?? string.Empty;
            _suppressEditorTextChanged = false;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_suppressEditorTextChanged)
        {
            return;
        }

        Pet.NotifyTyping();

        if (ViewModel?.SelectedTab is { } tab)
        {
            tab.Content = CodeEditor.Text;
        }
    }

    private void OnEditorCaretPositionChanged(object? sender, EventArgs e)
    {
        Pet.NotifyActivity();

        if (ViewModel?.SelectedTab is { } tab)
        {
            tab.Line = CodeEditor.TextArea.Caret.Line;
            tab.Column = CodeEditor.TextArea.Caret.Column;
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
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
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
