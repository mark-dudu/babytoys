namespace BabyToys.Models;

public sealed class ChildLockOptions
{
    public ImageSourceMode ImageSourceMode { get; init; }
    public string? CustomImagePath { get; init; }
    public TimeSpan Duration { get; init; }
    public bool ShowCountdown { get; init; }
    public TimeSpan ImagePhaseDuration { get; init; } = TimeSpan.FromSeconds(10);
}
