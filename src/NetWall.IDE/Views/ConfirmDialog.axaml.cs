using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NetWall.IDE.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public static Task<bool> AskAsync(Window owner, string message, string confirmText = "Delete")
    {
        var dialog = new ConfirmDialog();
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmText;
        return dialog.ShowDialog<bool>(owner);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
