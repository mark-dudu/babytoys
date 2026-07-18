using System.Globalization;
using BabyToys.Models;

namespace BabyToys.Services;

public static class SettingsPolicy
{
    public const double DefaultDurationMinutes = 5;
    public const double MinimumDurationMinutes = 0.5;
    public const double MaximumDurationMinutes = 240;

    public static bool TryParseDurationMinutes(string text, out double minutes)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out minutes) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out minutes);
    }

    public static double NormalizeDurationMinutes(double minutes)
    {
        if (double.IsNaN(minutes) || double.IsInfinity(minutes))
        {
            return DefaultDurationMinutes;
        }

        return Math.Clamp(minutes, MinimumDurationMinutes, MaximumDurationMinutes);
    }

    public static string FormatDurationMinutes(double minutes)
    {
        return NormalizeDurationMinutes(minutes).ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static AppSettings Normalize(AppSettings settings)
    {
        if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
        {
            throw new NotSupportedException(
                $"Settings schema {settings.SchemaVersion} is newer than supported schema {AppSettings.CurrentSchemaVersion}.");
        }

        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        settings.DurationMinutes = NormalizeDurationMinutes(settings.DurationMinutes);
        settings.Presets ??= [];

        var normalizedPresets = new List<ChildModePreset>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in settings.Presets)
        {
            if (preset is null)
            {
                continue;
            }

            var name = preset.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
            {
                continue;
            }

            preset.Name = name;
            if (!Enum.IsDefined(preset.ImageSourceMode))
            {
                preset.ImageSourceMode = ImageSourceMode.SystemWallpaper;
            }

            preset.DurationMinutes = NormalizeDurationMinutes(preset.DurationMinutes);
            normalizedPresets.Add(preset);
        }

        settings.Presets = normalizedPresets;
        if (!Enum.IsDefined(settings.ImageSourceMode))
        {
            settings.ImageSourceMode = ImageSourceMode.SystemWallpaper;
        }

        if (!string.IsNullOrWhiteSpace(settings.SelectedPresetName))
        {
            settings.SelectedPresetName = normalizedPresets
                .FirstOrDefault(preset => preset.Name.Equals(
                    settings.SelectedPresetName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                ?.Name;
        }
        else
        {
            settings.SelectedPresetName = null;
        }

        return settings;
    }
}
