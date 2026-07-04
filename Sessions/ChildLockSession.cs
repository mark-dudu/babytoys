using System.Windows;
using System.Windows.Threading;
using BabyToys.Models;
using BabyToys.Services;
using BabyToys.Views;
using Forms = System.Windows.Forms;

namespace BabyToys.Sessions;

public sealed class ChildLockSession : IDisposable
{
    private readonly ChildLockOptions _options;
    private readonly InputHookService _inputHookService;
    private readonly WallpaperService _wallpaperService;
    private readonly PowerService _powerService;
    private readonly List<ChildLockWindow> _windows = [];
    private readonly DispatcherTimer _imagePhaseTimer = new();
    private readonly DispatcherTimer _countdownTimer = new();
    private readonly DispatcherTimer _unlockHoldTimer = new();
    private DateTimeOffset _deadline;
    private bool _disposed;
    private bool _hasEnded;

    public ChildLockState State { get; private set; } = ChildLockState.Starting;

    public event EventHandler? Ended;

    public ChildLockSession(
        ChildLockOptions options,
        InputHookService inputHookService,
        WallpaperService wallpaperService,
        PowerService powerService)
    {
        _options = options;
        _inputHookService = inputHookService;
        _wallpaperService = wallpaperService;
        _powerService = powerService;

        _imagePhaseTimer.Interval = _options.ImagePhaseDuration;
        _imagePhaseTimer.Tick += (_, _) => SwitchToBlack();

        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.Tick += (_, _) => UpdateCountdownAndTimeout();

        _unlockHoldTimer.Interval = TimeSpan.FromSeconds(3);
        _unlockHoldTimer.Tick += (_, _) => Unlock();
    }

    public bool Start(Window owner)
    {
        State = ChildLockState.Starting;
        AppLogService.Current.Info("Child lock session starting.");

        if (!_inputHookService.TryInstall())
        {
            System.Windows.MessageBox.Show(
                owner,
                "无法安装键盘/鼠标拦截。请重试，或检查安全软件限制。",
                "无法进入儿童模式",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        _inputHookService.UnlockComboStateChanged += OnUnlockComboStateChanged;

        var imagePath = ResolveImagePath();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var window = new ChildLockWindow(screen.Bounds, imagePath, _options.ShowCountdown);
            window.Show();
            _windows.Add(window);
        }

        _deadline = DateTimeOffset.Now.Add(_options.Duration);
        State = ChildLockState.ActiveImage;
        _imagePhaseTimer.Start();
        _countdownTimer.Start();
        UpdateRemainingText();
        AppLogService.Current.Info("Child lock session active.");
        return true;
    }

    private string? ResolveImagePath()
    {
        if (_options.ImageSourceMode == ImageSourceMode.CustomImage &&
            !string.IsNullOrWhiteSpace(_options.CustomImagePath) &&
            File.Exists(_options.CustomImagePath))
        {
            return _options.CustomImagePath;
        }

        return _wallpaperService.GetCurrentWallpaperPath();
    }

    private void OnUnlockComboStateChanged(object? sender, bool isDown)
    {
        if (isDown)
        {
            State = ChildLockState.Unlocking;
            SetUnlockProgress(true);
            _unlockHoldTimer.Stop();
            _unlockHoldTimer.Start();
        }
        else
        {
            if (State == ChildLockState.Unlocking)
            {
                State = ChildLockState.ActiveBlack;
            }

            SetUnlockProgress(false);
            _unlockHoldTimer.Stop();
        }
    }

    private void SwitchToBlack()
    {
        _imagePhaseTimer.Stop();
        State = ChildLockState.ActiveBlack;
        foreach (var window in _windows)
        {
            window.FadeToBlack();
        }
    }

    private void UpdateCountdownAndTimeout()
    {
        if (DateTimeOffset.Now >= _deadline)
        {
            Timeout();
            return;
        }

        UpdateRemainingText();
    }

    private void UpdateRemainingText()
    {
        var remaining = _deadline - DateTimeOffset.Now;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        foreach (var window in _windows)
        {
            window.SetRemaining(remaining);
        }
    }

    private void SetUnlockProgress(bool isUnlocking)
    {
        foreach (var window in _windows)
        {
            window.SetUnlockProgress(isUnlocking);
        }
    }

    private void Timeout()
    {
        State = ChildLockState.Timeout;
        AppLogService.Current.Info("Child lock session timed out.");
        StopTimers();
        _inputHookService.UnlockComboStateChanged -= OnUnlockComboStateChanged;
        _inputHookService.Uninstall();

        State = ChildLockState.Sleeping;
        var sleepRequested = _powerService.TrySuspend();
        if (sleepRequested)
        {
            State = ChildLockState.Ended;
            AppLogService.Current.Info("System returned from successful sleep request; ending child lock session.");
            End(raiseEndedEvent: true);
            return;
        }
        else
        {
            State = ChildLockState.SleepFailedBlack;
            AppLogService.Current.Error("Sleep request failed; keeping black screen active.");
        }

        foreach (var window in _windows)
        {
            window.ShowBlackImmediately();
        }

        if (_inputHookService.TryInstall())
        {
            _inputHookService.UnlockComboStateChanged += OnUnlockComboStateChanged;
        }
        else
        {
            AppLogService.Current.Error("Failed to reinstall input hooks after sleep fallback.");
        }
    }

    private void Unlock()
    {
        State = ChildLockState.Ended;
        AppLogService.Current.Info("Child lock session unlocked.");
        End(raiseEndedEvent: true);
    }

    private void End(bool raiseEndedEvent)
    {
        if (_hasEnded)
        {
            return;
        }

        _hasEnded = true;
        StopTimers();
        _inputHookService.UnlockComboStateChanged -= OnUnlockComboStateChanged;
        _inputHookService.Uninstall();

        foreach (var window in _windows.ToArray())
        {
            window.Close();
        }

        _windows.Clear();
        if (raiseEndedEvent)
        {
            Ended?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StopTimers()
    {
        _imagePhaseTimer.Stop();
        _countdownTimer.Stop();
        _unlockHoldTimer.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        End(raiseEndedEvent: false);
        _inputHookService.Dispose();
        _disposed = true;
    }
}
