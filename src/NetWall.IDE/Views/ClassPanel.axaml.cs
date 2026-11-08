using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NetWall.IDE.Services;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Views;

public partial class ClassPanel : UserControl
{
    private const int NoticeMilliseconds = 5000;

    private int _noticeVersion;

    public ClassPanel()
    {
        InitializeComponent();
    }

    private async void OnOpenClassClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ClassViewModel viewModel || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Abrir clase",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Clases de NetWall") { Patterns = ["*.nwclase"] }],
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
            await ShowNoticeAsync(error.Message);
        }
    }

    private async Task ShowNoticeAsync(string message)
    {
        var version = ++_noticeVersion;
        NoticeText.Text = message;
        Notice.Classes.Set("shown", true);
        await Task.Delay(NoticeMilliseconds);
        if (version == _noticeVersion)
        {
            Notice.Classes.Set("shown", false);
        }
    }
}
