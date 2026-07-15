using System.Text.Json;
using System.Diagnostics;
using BabyToys.Models;

namespace BabyToys.Services;

public sealed class SessionRecoveryService
{
    public void MarkActive()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            var marker = new SessionMarker
            {
                StartedAt = DateTimeOffset.Now,
                ProcessId = Environment.ProcessId
            };
            File.WriteAllText(AppPaths.SessionMarkerPath, JsonSerializer.Serialize(marker));
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to write active session marker.", ex);
        }
    }

    public SessionMarker? ReadPreviousMarker()
    {
        try
        {
            if (!File.Exists(AppPaths.SessionMarkerPath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SessionMarker>(File.ReadAllText(AppPaths.SessionMarkerPath));
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to read active session marker.", ex);
            return null;
        }
    }

    public bool IsMarkerProcessActive(SessionMarker marker)
    {
        if (marker.ProcessId <= 0 || marker.ProcessId == Environment.ProcessId)
        {
            return marker.ProcessId == Environment.ProcessId;
        }

        try
        {
            using var process = Process.GetProcessById(marker.ProcessId);
            return !process.HasExited && process.ProcessName.Equals("BabyToys", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to validate active session process.", ex);
            return true;
        }
    }

    public bool IsAnotherSessionActive()
    {
        var marker = ReadPreviousMarker();
        return marker is not null &&
            marker.ProcessId != Environment.ProcessId &&
            IsMarkerProcessActive(marker);
    }

    public void ClearOwnedByCurrentProcess()
    {
        var marker = ReadPreviousMarker();
        if (marker?.ProcessId == Environment.ProcessId)
        {
            Clear();
        }
    }

    public void Clear()
    {
        try
        {
            File.Delete(AppPaths.SessionMarkerPath);
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to clear active session marker.", ex);
        }
    }
}
