using System.Text.Json;
using BabyToys.Models;

namespace BabyToys.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(AppPaths.SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Presets ??= [];
            return settings;
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to load settings.", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(AppPaths.SettingsPath, json);
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to save settings.", ex);
        }
    }
}
