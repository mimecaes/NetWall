using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lucide.Avalonia;
using NetWall.IDE.Models;

namespace NetWall.IDE.Views;

public enum DialogTone
{
    Danger,
    Warning,
}

public partial class ConfirmDialog : Window
{
    private enum Answer
    {
        Cancel,
        Secondary,
        Confirm,
    }

    private bool _closing;

    public ConfirmDialog()
    {
        InitializeComponent();
        Header.PointerPressed += (_, e) => BeginMoveDrag(e);
        KeyDown += OnKeyDown;
    }

    public static async Task<bool> AskAsync(Window owner, string title, string message, string confirmText,
        DialogTone tone)
    {
        var dialog = Create(title, message, tone);
        dialog.ConfirmButton.Content = confirmText;
        dialog.ConfirmButton.Classes.Add(tone == DialogTone.Danger ? "danger" : "primary");
        return await dialog.ShowDialog<Answer>(owner) == Answer.Confirm;
    }

    public static async Task<UnsavedChoice> AskUnsavedAsync(Window owner, string title, string message)
    {
        var dialog = Create(title, message, DialogTone.Warning);
        dialog.ToneIcon.Kind = LucideIconKind.Save;
        dialog.SecondaryButton.Content = "No guardar";
        dialog.SecondaryButton.IsVisible = true;
        dialog.ConfirmButton.Content = "Guardar";
        dialog.ConfirmButton.Classes.Add("primary");
        return await dialog.ShowDialog<Answer>(owner) switch
        {
            Answer.Confirm => UnsavedChoice.Save,
            Answer.Secondary => UnsavedChoice.Discard,
            _ => UnsavedChoice.Cancel,
        };
    }

    private static ConfirmDialog Create(string title, string message, DialogTone tone)
    {
        var dialog = new ConfirmDialog { Title = title };
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.Card.Classes.Add(tone == DialogTone.Danger ? "danger" : "warning");
        dialog.ToneIcon.Kind = tone == DialogTone.Danger ? LucideIconKind.Trash2 : LucideIconKind.TriangleAlert;
        return dialog;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        CancelButton.Focus();
        await Task.Delay(30);
        Card.Classes.Add("shown");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _ = CloseAnimatedAsync(Answer.Cancel);
        }
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => _ = CloseAnimatedAsync(Answer.Confirm);

    private void OnSecondaryClick(object? sender, RoutedEventArgs e) => _ = CloseAnimatedAsync(Answer.Secondary);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _ = CloseAnimatedAsync(Answer.Cancel);

    private async Task CloseAnimatedAsync(Answer answer)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Card.Classes.Add("closing");
        await Task.Delay(220);
        Close(answer);
    }
}
