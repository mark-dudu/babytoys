namespace BabyToys.Services;

public sealed class AppLogService
{
    private readonly object _gate = new();

    public static AppLogService Current { get; } = new();

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null)
    {
        var text = exception is null ? message : $"{message} {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", text);
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
                File.AppendAllText(filePath, line);
            }
        }
        catch
        {
            // Logging must never interfere with the lock session.
        }
    }
}
