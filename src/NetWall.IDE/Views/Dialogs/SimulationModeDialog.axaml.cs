using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NetWall.IDE.Controls;
using NetWall.IDE.Models;

namespace NetWall.IDE.Views.Dialogs;

public enum SimulationChoice
{
    None,
    Program,
    Rules,
    OpenFolder,
}

public partial class SimulationModeDialog : Window
{
    private const string Question = "¿Quieres entrar al modo simulación? Elige cómo quieres simular.";

    private bool _closing;
    private string _message = Question;

    public SimulationModeDialog()
    {
        InitializeComponent();
        Header.PointerPressed += (_, e) => BeginMoveDrag(e);
        KeyDown += OnKeyDown;
    }

    public static Task<SimulationChoice> ShowAsync(Window owner, PetColor helpyColor, string? problem, bool folderOpen)
    {
        var dialog = new SimulationModeDialog();
        dialog.Helpy.SetColor(helpyColor);
        if (problem is not null)
        {
            dialog._message = $"{problem} Para simular necesito un archivo .nw abierto. Te explico cómo:";
            dialog.Bubble.Classes.Add("bubble-warning");
            dialog.Options.IsVisible = false;
            dialog.NeedFile.IsVisible = true;
            dialog.FolderStep.Classes.Set("done", folderOpen);
            dialog.OpenFolderButton.IsVisible = !folderOpen;
        }

        return dialog.ShowDialog<SimulationChoice>(owner);
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await Task.Delay(30);
        Card.Classes.Add("shown");
        Helpy.Mood = HelpyMood.Talking;
        for (var i = 1; i <= _message.Length; i++)
        {
            BubbleText.Text = _message[..i];
            await Task.Delay(18);
        }

        Helpy.Mood = HelpyMood.Idle;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _ = CloseAnimatedAsync(SimulationChoice.None);
        }
    }

    private void OnProgramClick(object? sender, RoutedEventArgs e)
    {
        Helpy.Mood = HelpyMood.Happy;
        _ = CloseAnimatedAsync(SimulationChoice.Program);
    }

    private void OnRulesClick(object? sender, RoutedEventArgs e)
    {
        Helpy.Mood = HelpyMood.Happy;
        _ = CloseAnimatedAsync(SimulationChoice.Rules);
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e) =>
        _ = CloseAnimatedAsync(SimulationChoice.OpenFolder);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _ = CloseAnimatedAsync(SimulationChoice.None);

    private async Task CloseAnimatedAsync(SimulationChoice choice)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Card.Classes.Add("closing");
        await Task.Delay(220);
        Close(choice);
    }
}
