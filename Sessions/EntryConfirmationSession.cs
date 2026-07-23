using System.Windows.Threading;
using BabyToys.Models;
using BabyToys.Services;
using BabyToys.Views;
using Forms = System.Windows.Forms;

namespace BabyToys.Sessions;

public sealed class EntryConfirmationSession : IDisposable
{
    private readonly EntryConfirmationCountdown _countdown = new();
    private readonly DispatcherTimer _timer = new();
    private readonly List<EntryConfirmationWindow> _windows = [];
    private bool _started;
    private bool _disposed;

    public event EventHandler? Confirmed;
    public event EventHandler? Canceled;

    public EntryConfirmationSession()
    {
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTimerTick;
    }

    public bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            throw new InvalidOperationException("Entry confirmation is already running.");
        }

        try
        {
            var screens = Forms.Screen.AllScreens;
            if (screens.Length == 0)
            {
                return false;
            }

            foreach (var screen in screens)
            {
                var window = new EntryConfirmationWindow(screen.Bounds, screen.Primary);
                window.CancelRequested += OnCancelRequested;
                window.SetRemainingSeconds(_countdown.RemainingSeconds);
                _windows.Add(window);
                window.Show();
            }

            (_windows.FirstOrDefault(window => window.IsPrimary) ?? _windows[0])
                .ActivateCancellationControls();
            _started = true;
            _timer.Start();
            AppLogService.Current.Info("Low-stimulation entry confirmation active.");
            return true;
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to create entry confirmation windows.", ex);
            CloseWindows();
            return false;
        }
    }

    public void Cancel()
    {
        if (_disposed || _countdown.State != EntryConfirmationState.Waiting)
        {
            return;
        }

        _countdown.Cancel();
        _timer.Stop();
        CloseWindows();
        AppLogService.Current.Info("Entry confirmation canceled.");
        Canceled?.Invoke(this, EventArgs.Empty);
    }

    public void PrepareForHandoff()
    {
        _timer.Stop();
        foreach (var window in _windows)
        {
            window.Topmost = false;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_countdown.Tick() == EntryConfirmationState.Confirmed)
        {
            PrepareForHandoff();
            AppLogService.Current.Info("Entry confirmation completed.");
            Confirmed?.Invoke(this, EventArgs.Empty);
            return;
        }

        foreach (var window in _windows)
        {
            window.SetRemainingSeconds(_countdown.RemainingSeconds);
        }
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        Cancel();
    }

    private void CloseWindows()
    {
        foreach (var window in _windows.ToArray())
        {
            window.CancelRequested -= OnCancelRequested;
            try
            {
                window.CloseForCleanup();
            }
            catch (Exception ex)
            {
                AppLogService.Current.Error("Failed to close an entry confirmation window.", ex);
            }
        }

        _windows.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        CloseWindows();
        _disposed = true;
    }
}
