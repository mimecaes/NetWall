using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NetWall.IDE.Localization;

namespace NetWall.IDE.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public static Task<bool> AskAsync(Window owner, string message, string? confirmText = null)
    {
        var dialog = new ConfirmDialog
        {
            Title = LocalizationService.T("Dialog.ConfirmTitle"),
        };
        dialog.MessageText.Text = message;
        dialog.CancelButton.Content = LocalizationService.T("Dialog.Cancel");
        dialog.ConfirmButton.Content = confirmText ?? LocalizationService.T("Dialog.Delete");
        return dialog.ShowDialog<bool>(owner);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
