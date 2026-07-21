using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BabyToys.Services;

public sealed class PowerService : IDisposable
{
    private bool _isMonitoring;
    private bool _disposed;

    public event EventHandler? Resumed;
    public event EventHandler? DisplaySettingsChanged;

    public bool TryStartMonitoring()
    {
        if (_isMonitoring)
        {
            return true;
        }

        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            _isMonitoring = true;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            AppLogService.Current.Info("Power and display change monitoring started.");
            return true;
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to start power and display change monitoring.", ex);
            StopMonitoring();
            return false;
        }
    }

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

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        AppLogService.Current.Info("System resume notification received.");
        Resumed?.Invoke(this, EventArgs.Empty);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        AppLogService.Current.Info("Display settings change notification received.");
        DisplaySettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StopMonitoring()
    {
        if (!_isMonitoring)
        {
            return;
        }

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _isMonitoring = false;
        AppLogService.Current.Info("Power and display change monitoring stopped.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopMonitoring();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}
