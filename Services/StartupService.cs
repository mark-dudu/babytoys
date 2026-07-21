using Microsoft.Win32;

namespace BabyToys.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BabyToys";

    public bool MatchesDesiredState(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var registeredCommand = key?.GetValue(ValueName) as string;
            return StartupRegistrationPolicy.MatchesDesiredState(
                registeredCommand,
                Environment.ProcessPath,
                enabled);
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to read startup registration.", ex);
            return false;
        }
    }

    public bool TrySetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var executablePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return false;
                }

                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                if (key is null)
                {
                    return false;
                }

                key.SetValue(ValueName, StartupRegistrationPolicy.BuildCommand(executablePath));
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key is null)
                {
                    return true;
                }

                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to update startup registration.", ex);
            return false;
        }
    }
}
