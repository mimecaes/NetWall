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
using AvaloniaEdit.Document;
using NetWall.IDE.Controls;
using NetWall.IDE.Models;
using NetWall.IDE.Themes;
using NetWall.IDE.ViewModels;
using NetWall.IDE.Views.Dialogs;

namespace NetWall.IDE.Views;

public partial class MainWindow : Window
{
    private bool _suppressEditorTextChanged;
    private readonly IndentGuideRenderer _indentGuideRenderer = new();
    private readonly IndentRainbowRenderer _indentRainbowRenderer = new();
    private readonly NetWallSyntaxColorizer _syntaxColorizer = new();
    private DispatcherTimer? _indentFadeTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        CodeEditor.TextChanged += OnEditorTextChanged;
        CodeEditor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_indentGuideRenderer);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_indentRainbowRenderer);
        CodeEditor.TextArea.TextView.LineTransformers.Add(_syntaxColorizer);
        TerminalScrollViewer.AddHandler(AnsiTextBlock.LinkClickedEvent, OnTerminalLinkClicked);
        ThemeManager.ThemeApplied += OnThemeApplied;
        Closed += (_, _) => ThemeManager.ThemeApplied -= OnThemeApplied;
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
            vm.RequestCodeAssistant += OnRequestCodeAssistant;
            vm.RequestSaveAs += OnRequestSaveAs;
            vm.RequestDeleteConfirmation += OnRequestDeleteConfirmation;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.TerminalUpdated += OnTerminalUpdated;
            vm.RequestRevealLine += OnRequestRevealLine;
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
        if (e.PropertyName == nameof(MainViewModel.SelectedTab))
        {
            _suppressEditorTextChanged = true;
            _syntaxColorizer.IsEnabled = ViewModel?.SelectedTab?.Language == "NetWall";
            CodeEditor.Text = ViewModel?.SelectedTab?.Content ?? string.Empty;
            _suppressEditorTextChanged = false;
        }
    }

    private void OnTerminalUpdated() =>
        Dispatcher.UIThread.Post(TerminalScrollViewer.ScrollToEnd, DispatcherPriority.Background);

    private void OnTerminalLinkClicked(object? sender, TerminalLinkEventArgs e)
    {
        if (!e.Uri.IsFile)
        {
            return;
        }

        // Diagnostic links look like file:///C:/project/firewall.nw#L12.
        var fragment = e.Uri.Fragment.TrimStart('#').TrimStart('L');
        var line = int.TryParse(fragment, out var parsed) ? parsed : 1;
        ViewModel?.OpenFileAtLine(e.Uri.LocalPath, line);
    }

    private void OnRequestRevealLine(int line)
    {
        var lineNumber = Math.Clamp(line, 1, Math.Max(1, CodeEditor.Document.LineCount));
        CodeEditor.TextArea.Caret.Location = new AvaloniaEdit.Document.TextLocation(lineNumber, 1);
        CodeEditor.ScrollToLine(lineNumber);
        CodeEditor.TextArea.Focus();
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

    private async void OnRequestCodeAssistant()
    {
        var code = await CodeAssistantDialog.ShowAsync(this, Pet.Color, ViewModel?.SelectedTab?.FilePath, ViewModel?.RootPath,
            CodeEditor.Text);
        if (code is not null)
        {
            InsertAtCaret(code);
        }
    }

    private void InsertAtCaret(string code)
    {
        var document = CodeEditor.Document;
        var caret = CodeEditor.TextArea.Caret;
        var line = document.GetLineByNumber(caret.Line);
        var newLine = TextUtilities.GetNewLineFromDocument(document, caret.Line);
        var indent = document.GetText(TextUtilities.GetLeadingWhitespace(document, line));
        var beforeCaret = document.GetText(line.Offset, CodeEditor.CaretOffset - line.Offset);
        var afterCaret = document.GetText(CodeEditor.CaretOffset, line.EndOffset - CodeEditor.CaretOffset);

        // Every generated line keeps the indentation of the current line. The block starts on
        // a new line if there is code before the caret, and ends with one if there is code after.
        var text = string.Join(newLine + indent, code.TrimEnd('\n').Split('\n'));
        if (!string.IsNullOrWhiteSpace(afterCaret))
        {
            text += newLine;
        }
        if (!string.IsNullOrWhiteSpace(beforeCaret))
        {
            text = newLine + indent + text;
        }
        else if (beforeCaret.Length < indent.Length)
        {
            text = indent[beforeCaret.Length..] + text;
        }

        document.Insert(CodeEditor.CaretOffset, text);
        CodeEditor.TextArea.Focus();
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

    private void OnThemeApplied(object? sender, EventArgs e) => CodeEditor.TextArea.TextView.Redraw();

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
