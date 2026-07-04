namespace BabyToys.Models;

public sealed class AppSettings
{
    public ImageSourceMode ImageSourceMode { get; set; } = ImageSourceMode.SystemWallpaper;
    public string? CustomImagePath { get; set; }
    public double DurationMinutes { get; set; } = 5;
    public bool ShowCountdown { get; set; }
}
