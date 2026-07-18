namespace BabyToys.Services;

public sealed class AppLogService
{
    private const long MaximumLogFileBytes = 2 * 1024 * 1024;
    private const int MaximumRetainedLogFiles = 14;
    private readonly object _gate = new();
    private bool _retentionApplied;

    public static AppLogService Current { get; } = new();

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null)
    {
        var text = exception is null ? message : $"{message} {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", text);
    }

    public bool TryOpenLogsDirectory()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{AppPaths.LogsDirectory}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Error("Failed to open logs directory.", ex);
            return false;
        }
    }

    private void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            var filePath = Path.Combine(AppPaths.LogsDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var line = $"{DateTime.Now:O} [{level}] {message}{Environment.NewLine}";

            lock (_gate)
            {
                var rotated = RotateIfNeeded(filePath);
                File.AppendAllText(filePath, line);
                if (rotated || !_retentionApplied)
                {
                    ApplyRetention();
                    _retentionApplied = true;
                }
            }
        }
        catch
        {
            // Logging must never interfere with the lock session.
        }
    }

    private static bool RotateIfNeeded(string filePath)
    {
        if (!File.Exists(filePath) || new FileInfo(filePath).Length < MaximumLogFileBytes)
        {
            return false;
        }

        var rotatedPath = Path.Combine(
            AppPaths.LogsDirectory,
            $"{Path.GetFileNameWithoutExtension(filePath)}-{DateTime.Now:HHmmss}-{Guid.NewGuid():N}.log");
        File.Move(filePath, rotatedPath);
        return true;
    }

    private static void ApplyRetention()
    {
        var expiredFiles = new DirectoryInfo(AppPaths.LogsDirectory)
            .EnumerateFiles("*.log")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Skip(MaximumRetainedLogFiles);

        foreach (var file in expiredFiles)
        {
            file.Delete();
        }
    }
}
