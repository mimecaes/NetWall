using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NetWall.IDE.Models;

namespace NetWall.IDE.Views;

public partial class CatGalleryPanel : UserControl
{
    public CatGalleryPanel()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    public void SetHelpyColor(PetColor color) => Helpy.SetColor(color);

    public async Task ShowAsync()
    {
        Border[] photos = [Maca, Milka, Simba];
        foreach (var photo in photos)
        {
            photo.Classes.Set("shown", false);
            photo.Margin = new Thickness(0, -60, 0, 60);
        }

        IsVisible = true;
        Focus();
        await Task.Delay(30);
        Backdrop.Classes.Set("shown", true);
        foreach (var photo in photos)
        {
            await Task.Delay(140);
            photo.Classes.Set("shown", true);
            photo.Margin = default;
        }
    }

    private async Task HideAsync()
    {
        Backdrop.Classes.Set("shown", false);
        foreach (var photo in new[] { Maca, Milka, Simba })
        {
            photo.Classes.Set("shown", false);
        }

        await Task.Delay(300);
        IsVisible = false;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsVisible)
        {
            e.Handled = true;
            _ = HideAsync();
        }
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e) => _ = HideAsync();
}
