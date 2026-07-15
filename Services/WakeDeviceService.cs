using System.Diagnostics;

namespace BabyToys.Services;

public sealed class WakeDeviceService
{
    public async Task<IReadOnlyList<string>> GetArmedDevicesAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/devicequery wake_armed",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return [];
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                AppLogService.Current.Error($"powercfg wake device query failed: {error.Trim()}");
                return [];
            }

            return output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !line.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to query wake devices.", ex);
            return [];
        }
    }
}
