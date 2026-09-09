using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NetWall.IDE.Localization;
using NetWall.IDE.Services;
using NetWall.IDE.Themes;
using NetWall.IDE.ViewModels;
using NetWall.IDE.Views;

namespace NetWall.IDE;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = AppSettingsService.Load();
            LocalizationService.Instance.Initialize(settings.Language);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
            ThemeManager.Apply(settings.Theme);
        }

        base.OnFrameworkInitializationCompleted();
    }
}