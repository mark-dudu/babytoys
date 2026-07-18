namespace BabyToys.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ImageSourceMode ImageSourceMode { get; set; } = ImageSourceMode.SystemWallpaper;
    public string? CustomImagePath { get; set; }
    public double DurationMinutes { get; set; } = 5;
    public bool ShowCountdown { get; set; }
    public bool StartWithWindows { get; set; }
    public bool EnableGlobalHotKey { get; set; } = true;
    public string? SelectedPresetName { get; set; }
    public List<ChildModePreset> Presets { get; set; } = [];
}
