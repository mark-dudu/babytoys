using System.Text.Json;
using BabyToys.Models;

namespace BabyToys.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Action<string, Exception?> _logError;
    private readonly Action<string> _logInfo;

    public bool IsReadOnlyDueToUnsupportedSchema { get; private set; }
    public int? UnsupportedSchemaVersion { get; private set; }

    public SettingsService(
        string? settingsPath = null,
        Func<DateTimeOffset>? clock = null,
        Action<string, Exception?>? logError = null,
        Action<string>? logInfo = null)
    {
        _settingsPath = settingsPath ?? AppPaths.SettingsPath;
        _clock = clock ?? (() => DateTimeOffset.Now);
        _logError = logError ?? AppLogService.Current.Error;
        _logInfo = logInfo ?? AppLogService.Current.Info;
    }

    public AppSettings Load()
    {
        IsReadOnlyDueToUnsupportedSchema = false;
        UnsupportedSchemaVersion = null;

        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            using var document = JsonDocument.Parse(json);
            var hasSchemaVersion = document.RootElement.TryGetProperty(nameof(AppSettings.SchemaVersion), out _);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            if (!hasSchemaVersion)
            {
                settings.SchemaVersion = 0;
            }

            if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
            {
                IsReadOnlyDueToUnsupportedSchema = true;
                UnsupportedSchemaVersion = settings.SchemaVersion;
                _logError(
                    $"Settings schema {settings.SchemaVersion} is newer than supported schema {AppSettings.CurrentSchemaVersion}; preserving the file without changes.",
                    null);
                return new AppSettings();
            }

            return SettingsPolicy.Normalize(settings);
        }
        catch (Exception ex)
        {
            _logError("Failed to load settings.", ex);
            TryBackupInvalidSettings();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        if (IsReadOnlyDueToUnsupportedSchema)
        {
            _logError(
                $"Settings save skipped because schema {UnsupportedSchemaVersion} is newer than this application supports.",
                null);
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Settings path must have a parent directory.");
            }

            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(SettingsPolicy.Normalize(settings), JsonOptions);
            var temporaryPath = $"{_settingsPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(_settingsPath))
                {
                    File.Replace(temporaryPath, _settingsPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temporaryPath, _settingsPath);
                }
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }
        catch (Exception ex)
        {
            _logError("Failed to save settings.", ex);
        }
    }

    private void TryBackupInvalidSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(_settingsPath)!;
            var fileName = Path.GetFileNameWithoutExtension(_settingsPath);
            var extension = Path.GetExtension(_settingsPath);
            var timestamp = _clock().ToString("yyyyMMdd-HHmmss");
            var backupPath = Path.Combine(directory, $"{fileName}.corrupt-{timestamp}{extension}");
            if (File.Exists(backupPath))
            {
                backupPath = Path.Combine(directory, $"{fileName}.corrupt-{timestamp}-{Guid.NewGuid():N}{extension}");
            }

            File.Move(_settingsPath, backupPath);
            _logInfo($"Invalid settings backed up to {Path.GetFileName(backupPath)}.");
        }
        catch (Exception ex)
        {
            _logError("Failed to back up invalid settings.", ex);
        }
    }
}
