using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Nebula;
    public AppLanguage Language { get; set; } = AppLanguage.Spanish;
}

public static class AppSettingsService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NetWall.IDE"
    );

    private static readonly string SettingsPath = Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
        }
    }
}
