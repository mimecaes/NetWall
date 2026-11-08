using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace NetWall.IDE.Views;

public partial class ConfirmDialog : Window
{
    private bool _closing;

    public ConfirmDialog()
    {
        InitializeComponent();
        Header.PointerPressed += (_, e) => BeginMoveDrag(e);
        KeyDown += OnKeyDown;
    }

    public static Task<bool> AskAsync(Window owner, string title, string before, string name, string after,
        string confirmText = "Eliminar")
    {
        var dialog = new ConfirmDialog();
        dialog.TitleText.Text = title;
        dialog.MessageText.Inlines =
        [
            new Run(before),
            new Run(name) { FontWeight = FontWeight.SemiBold, Foreground = dialog.TitleText.Foreground },
            new Run(after),
        ];
        dialog.ConfirmButton.Content = confirmText;
        return dialog.ShowDialog<bool>(owner);
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await Task.Delay(30);
        Card.Classes.Add("shown");
        ConfirmButton.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _ = CloseAnimatedAsync(false);
        }
        else if (e.Key == Key.Enter)
        {
            _ = CloseAnimatedAsync(true);
        }
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => _ = CloseAnimatedAsync(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _ = CloseAnimatedAsync(false);

    private async Task CloseAnimatedAsync(bool result)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Card.Classes.Add("closing");
        await Task.Delay(200);
        Close(result);
    }
}
