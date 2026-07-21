namespace BabyToys.Services;

public static class StartupRegistrationPolicy
{
    public static string BuildCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{executablePath}\" --minimized";
    }

    public static bool MatchesDesiredState(
        string? registeredCommand,
        string? executablePath,
        bool enabled)
    {
        if (!enabled)
        {
            return string.IsNullOrWhiteSpace(registeredCommand);
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        return string.Equals(
            registeredCommand?.Trim(),
            BuildCommand(executablePath),
            StringComparison.OrdinalIgnoreCase);
    }
}
