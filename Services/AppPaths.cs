namespace BabyToys.Services;

public static class AppPaths
{
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BabyToys");

    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");

    public static string LogsDirectory => Path.Combine(AppDataDirectory, "logs");

    public static string SessionMarkerPath => Path.Combine(AppDataDirectory, "active-session.json");
}
