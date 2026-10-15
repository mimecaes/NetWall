using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NetWall.IDE.ViewModels;

namespace NetWall.IDE.Views.Dialogs;

public partial class CodeAssistantDialog : Window
{
    public CodeAssistantDialog()
    {
        InitializeComponent();
        DataContext = new CodeAssistantViewModel();
    }

    /// <summary>Shows the assistant and returns the generated code, or null if it was cancelled.</summary>
    public static Task<string?> ShowAsync(Window owner, string? currentFile, string? projectRoot, string documentText)
    {
        var dialog = new CodeAssistantDialog
        {
            DataContext = new CodeAssistantViewModel(currentFile, projectRoot, documentText),
        };
        return dialog.ShowDialog<string?>(owner);
    }

    private void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CodeAssistantViewModel { CanInsert: true } viewModel)
        {
            Close(viewModel.GenerateCode());
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
