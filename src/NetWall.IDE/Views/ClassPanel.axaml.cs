using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Lucide.Avalonia;
using NetWall.IDE.Models;
using NetWall.IDE.Services;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Views;

public partial class ClassPanel : UserControl
{
    private const int NoticeMilliseconds = 5000;
    private const long MaxNwBytes = 512 * 1024;

    private static readonly FilePickerFileType ClassFiles = new("Clases de NetWall") { Patterns = ["*.nwclase"] };

    private int _noticeVersion;
    private ExerciseEditor? _dragged;
    private ClassViewModel? _wired;

    public ClassPanel()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPanelKeyDown, RoutingStrategies.Tunnel);
    }

    private ClassViewModel? ViewModel => DataContext as ClassViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_wired is not null)
        {
            _wired.Join.Joined -= OnJoined;
            _wired.Join.Failed -= OnJoinFailed;
            _wired.Join.Ended -= OnClassEnded;
        }

        _wired = ViewModel;
        if (_wired is not null)
        {
            _wired.Join.Joined += OnJoined;
            _wired.Join.Failed += OnJoinFailed;
            _wired.Join.Ended += OnClassEnded;
        }
    }

    private void OnJoined() => ViewModel?.ShowStudent();

    private void OnJoinFailed(string message)
    {
        CodeBoxShell.Classes.Set("shake", false);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => CodeBoxShell.Classes.Set("shake", true),
            Avalonia.Threading.DispatcherPriority.Background);
        _ = ShowNoticeAsync(message, false);
    }

    private void OnClassEnded(string message)
    {
        ViewModel?.StartJoining();
        _ = ShowNoticeAsync(message, false);
    }

    private void OnOpenRoomClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel?.OpenRoom();
        }
        catch (ClassFileException error)
        {
            _ = ShowNoticeAsync(error.Message, false);
        }
    }

    private void OnCloseRoomClick(object? sender, RoutedEventArgs e) => ViewModel?.CloseRoom();

    private void OnVoteChosen(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ClassExercise exercise } control)
        {
            return;
        }

        if (control.FindLogicalAncestorOfType<Popup>() is { } popup)
        {
            popup.IsOpen = false;
        }

        ViewModel?.Room.StartVoteCommand.Execute(exercise);
    }

    private void OnJoinClassClick(object? sender, RoutedEventArgs e) => ViewModel?.StartJoining();

    private void OnBackClick(object? sender, RoutedEventArgs e) => ViewModel?.BackToStart();

    private void OnEditorBackClick(object? sender, RoutedEventArgs e) => ViewModel?.RunOrConfirm(ViewModel.CloseClass);

    private void OnNewClassClick(object? sender, RoutedEventArgs e) => ViewModel?.RunOrConfirm(ViewModel.NewClass);

    private void OnOpenClassClick(object? sender, RoutedEventArgs e) => ViewModel?.RunOrConfirm(() => _ = OpenClassAsync());

    private async Task OpenClassAsync()
    {
        if (ViewModel is not { } viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Abrir clase",
            AllowMultiple = false,
            FileTypeFilter = [ClassFiles],
        });
        if (files is not [var file] || file.TryGetLocalPath() is not { } path)
        {
            return;
        }

        try
        {
            await viewModel.LoadAsync(path);
        }
        catch (ClassFileException error)
        {
            await ShowNoticeAsync(error.Message, false);
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = SaveAsync();

    private async Task SaveAsync()
    {
        if (ViewModel is not { IsEditing: true } viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var path = viewModel.FilePath;
        if (path is null)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Guardar clase",
                SuggestedFileName = $"{viewModel.Name.Trim()}.nwclase",
                DefaultExtension = "nwclase",
                FileTypeChoices = [ClassFiles],
            });
            path = file?.TryGetLocalPath();
            if (path is null)
            {
                return;
            }
        }

        try
        {
            viewModel.Save(path);
            await ShowNoticeAsync($"Clase guardada en {Path.GetFileName(path)}.", true);
        }
        catch (ClassFileException error)
        {
            await ShowNoticeAsync(error.Message, false);
        }
    }

    private async void OnUseNwClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.Selected is not { } exercise || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Usar un archivo .nw",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Archivos NetWall") { Patterns = ["*.nw"] }],
        });
        if (files is not [var file] || file.TryGetLocalPath() is not { } path)
        {
            return;
        }

        try
        {
            if (new FileInfo(path).Length > MaxNwBytes)
            {
                await ShowNoticeAsync("El archivo .nw es demasiado grande para un ejercicio.", false);
                return;
            }

            exercise.Starter = await File.ReadAllTextAsync(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            await ShowNoticeAsync("No se pudo leer el archivo .nw.", false);
        }
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control) && ViewModel is { IsEditing: true })
        {
            _ = SaveAsync();
            e.Handled = true;
        }
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ExerciseEditor exercise } && !exercise.IsSelected)
        {
            exercise.SelectCommand.Execute(null);
        }
    }

    private void OnStudentCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: StudentExercise exercise } && !exercise.IsSelected)
        {
            exercise.SelectCommand.Execute(null);
        }
    }

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: ExerciseEditor exercise } grip)
        {
            return;
        }

        _dragged = exercise;
        exercise.IsDragging = true;
        e.Pointer.Capture(grip);
        e.Handled = true;
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged is not { } exercise || ViewModel is not { } viewModel)
        {
            return;
        }

        var y = e.GetPosition(ExerciseList).Y;
        for (var i = 0; i < viewModel.Exercises.Count; i++)
        {
            if (ExerciseList.ContainerFromIndex(i) is { } container && container.Bounds.Contains(new Point(1, y)))
            {
                viewModel.Move(viewModel.Exercises.IndexOf(exercise), i);
                return;
            }
        }
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragged is { } exercise)
        {
            exercise.IsDragging = false;
        }

        _dragged = null;
        e.Pointer.Capture(null);
    }

    private async Task ShowNoticeAsync(string message, bool ok)
    {
        var version = ++_noticeVersion;
        NoticeText.Text = message;
        NoticeIcon.Kind = ok ? LucideIconKind.CircleCheck : LucideIconKind.CircleAlert;
        NoticeIcon.Stroke = this.FindResource(ok ? "RunGreenBrush" : "StopRedBrush") as Avalonia.Media.IBrush;
        Notice.Classes.Set("ok", ok);
        Notice.Classes.Set("shown", true);
        await Task.Delay(NoticeMilliseconds);
        if (version == _noticeVersion)
        {
            Notice.Classes.Set("shown", false);
        }
    }
}
