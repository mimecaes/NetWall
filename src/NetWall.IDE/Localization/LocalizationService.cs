using System.ComponentModel;
using NetWall.IDE.Models;
using NetWall.IDE.Services;

namespace NetWall.IDE.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private AppLanguage _language = AppLanguage.Spanish;

    private LocalizationService()
    {
    }

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            var settings = AppSettingsService.Load();
            settings.Language = value;
            AppSettingsService.Save(settings);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    /// <summary>Sets the initial language read from disk without re-persisting it.</summary>
    public void Initialize(AppLanguage language) => _language = language;

    public string this[string key] => Strings.Translations.TryGetValue(key, out var pair)
        ? (_language == AppLanguage.Spanish ? pair.Es : pair.En)
        : key;

    public static string T(string key) => Instance[key];
}
