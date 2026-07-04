using System.Runtime.InteropServices;

namespace BabyToys.Services;

public sealed class PowerService
{
    public bool TrySuspend()
    {
        try
        {
            AppLogService.Current.Info("Requesting system sleep.");
            return SetSuspendState(false, false, false);
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to request system sleep.", ex);
            return false;
        }
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}
