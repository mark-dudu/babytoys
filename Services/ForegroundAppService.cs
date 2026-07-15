using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BabyToys.Services;

public sealed class ForegroundAppService
{
    public bool IsTaskManagerForeground()
    {
        try
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero)
            {
                return false;
            }

            GetWindowThreadProcessId(window, out var processId);
            if (processId == 0)
            {
                return false;
            }

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName.Equals("Taskmgr", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
