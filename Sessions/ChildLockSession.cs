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
    private readonly SessionRecoveryService _recoveryService = new();
    private readonly ForegroundAppService _foregroundAppService = new();
    private readonly List<ChildLockWindow> _windows = [];
    private readonly DispatcherTimer _imagePhaseTimer = new();
    private readonly DispatcherTimer _countdownTimer = new();
    private readonly DispatcherTimer _unlockHoldTimer = new();
    private readonly DispatcherTimer _recoveryMonitorTimer = new();
    private readonly DispatcherTimer _sleepRecoveryTimer = new();
    private DateTimeOffset _deadline;
    private bool _disposed;
    private bool _hasEnded;
    private bool _isPostTimeoutProtection;
    private bool _unlockHoldCompleted;
    private DateTimeOffset _unlockHoldStartedAt;

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

        _unlockHoldTimer.Interval = TimeSpan.FromMilliseconds(100);
        _unlockHoldTimer.Tick += (_, _) => UpdateUnlockHold();

        _recoveryMonitorTimer.Interval = TimeSpan.FromMilliseconds(250);
        _recoveryMonitorTimer.Tick += (_, _) => CheckEmergencyRecovery();

        _sleepRecoveryTimer.Interval = TimeSpan.FromSeconds(8);
        _sleepRecoveryTimer.Tick += (_, _) => RecoverAfterSleep("sleep request watchdog");

        _powerService.Resumed += OnSystemResumed;
        _powerService.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public bool Start(Window owner)
    {
        State = ChildLockState.Starting;
        AppLogService.Current.Info("Child lock session starting.");

        if (_recoveryService.IsAnotherSessionActive())
        {
            System.Windows.MessageBox.Show(
                owner,
                "另一个 BabyToys 实例正在运行儿童模式。请先在原实例中解锁或退出。",
                "无法进入儿童模式",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        if (!_powerService.TryStartMonitoring())
        {
            System.Windows.MessageBox.Show(
                owner,
                "无法监测睡眠恢复和显示器变化，已取消进入儿童模式。请打开诊断查看日志。",
                "无法进入儿童模式",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

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

        try
        {
            var startsBlack = _options.EntryVisualMode == EntryVisualMode.ImmediateBlack;
            var imagePath = startsBlack ? null : ResolveImagePath();
            CreateWindows(imagePath, startsBlack);

            _deadline = DateTimeOffset.Now.Add(_options.Duration);
            _recoveryService.MarkActive();
            State = startsBlack ? ChildLockState.ActiveBlack : ChildLockState.ActiveImage;
            if (!startsBlack)
            {
                _imagePhaseTimer.Start();
            }
            _countdownTimer.Start();
            _recoveryMonitorTimer.Start();
            UpdateRemainingText();
            AppLogService.Current.Info("Child lock session active.");
            return true;
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed while creating child lock session windows.", ex);
            End(raiseEndedEvent: false);
            System.Windows.MessageBox.Show(
                owner,
                "儿童模式启动过程中发生错误，已安全释放输入拦截。请打开诊断查看日志。",
                "无法进入儿童模式",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
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
            _unlockHoldCompleted = false;
            _unlockHoldStartedAt = DateTimeOffset.Now;
            UpdateUnlockProgress(TimeSpan.Zero, isReady: false);
            _unlockHoldTimer.Stop();
            _unlockHoldTimer.Start();
        }
        else
        {
            var shouldUnlock = _unlockHoldCompleted;
            _unlockHoldCompleted = false;
            if (State == ChildLockState.Unlocking)
            {
                State = ChildLockState.ActiveBlack;
            }

            HideUnlockProgress();
            _unlockHoldTimer.Stop();
            if (shouldUnlock)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(Unlock);
            }
        }
    }

    private void UpdateUnlockHold()
    {
        var elapsed = DateTimeOffset.Now - _unlockHoldStartedAt;
        if (ChildLockPolicy.IsUnlockReady(elapsed))
        {
            _unlockHoldTimer.Stop();
            _unlockHoldCompleted = true;
            UpdateUnlockProgress(ChildLockPolicy.UnlockHoldDuration, isReady: true);
            return;
        }

        UpdateUnlockProgress(elapsed, isReady: false);
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
        var remaining = ChildLockPolicy.GetRemaining(_deadline, DateTimeOffset.Now);

        foreach (var window in _windows)
        {
            window.SetRemaining(remaining);
        }
    }

    private void UpdateUnlockProgress(TimeSpan elapsed, bool isReady)
    {
        foreach (var window in _windows)
        {
            window.SetUnlockProgress(elapsed, isReady);
        }
    }

    private void HideUnlockProgress()
    {
        foreach (var window in _windows)
        {
            window.HideUnlockProgress();
        }
    }

    private void Timeout()
    {
        State = ChildLockState.Timeout;
        _isPostTimeoutProtection = true;
        AppLogService.Current.Info("Child lock session timed out.");
        StopTimers();
        _inputHookService.UnlockComboStateChanged -= OnUnlockComboStateChanged;
        _inputHookService.Uninstall();

        foreach (var window in _windows)
        {
            window.EnterPersistentBlackMode();
        }

        State = ChildLockState.Sleeping;
        var sleepRequested = _powerService.TrySuspend();
        State = ChildLockPolicy.GetStateAfterSleepRequest(sleepRequested);
        if (State == ChildLockState.Sleeping)
        {
            AppLogService.Current.Info("Sleep request accepted; waiting for system resume.");
            _sleepRecoveryTimer.Start();
            return;
        }

        AppLogService.Current.Error("Sleep request failed; keeping black screen active.");
        RestoreBlackProtection("failed sleep request", ChildLockState.SleepFailedBlack);
    }

    private void OnSystemResumed(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            () => RecoverAfterSleep("system resume notification"));
    }

    private void RecoverAfterSleep(string reason)
    {
        _sleepRecoveryTimer.Stop();
        if (_hasEnded || State != ChildLockState.Sleeping)
        {
            return;
        }

        AppLogService.Current.Info($"Restoring child lock after {reason}.");
        RestoreBlackProtection(reason, ChildLockState.ActiveBlack);
    }

    private void RestoreBlackProtection(string reason, ChildLockState restoredState)
    {
        try
        {
            ReplaceWindows(imagePath: null, startsBlack: true);
            SuppressPostTimeoutCountdown();
            if (!_inputHookService.TryInstall())
            {
                throw new InvalidOperationException("Input hooks could not be installed.");
            }

            _inputHookService.UnlockComboStateChanged -= OnUnlockComboStateChanged;
            _inputHookService.UnlockComboStateChanged += OnUnlockComboStateChanged;
            State = restoredState;
            _recoveryMonitorTimer.Start();
            AppLogService.Current.Info($"Child lock protection restored after {reason}.");
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error($"Failed to restore child lock protection after {reason}; ending safely.", ex);
            End(raiseEndedEvent: true);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(RefreshDisplayCoverage);
    }

    private void RefreshDisplayCoverage()
    {
        if (_hasEnded || State == ChildLockState.Sleeping)
        {
            return;
        }

        try
        {
            var startsBlack = State != ChildLockState.ActiveImage;
            var imagePath = startsBlack ? null : ResolveImagePath();
            ReplaceWindows(imagePath, startsBlack);
            SuppressPostTimeoutCountdown();
            UpdateRemainingText();
            AppLogService.Current.Info("Child lock display coverage refreshed.");
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to refresh child lock display coverage; ending safely.", ex);
            End(raiseEndedEvent: true);
        }
    }

    private void CreateWindows(string? imagePath, bool startsBlack)
    {
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var window = new ChildLockWindow(screen.Bounds, imagePath, _options.ShowCountdown, startsBlack);
            window.Show();
            _windows.Add(window);
        }
    }

    private void ReplaceWindows(string? imagePath, bool startsBlack)
    {
        var replacements = new List<ChildLockWindow>();
        try
        {
            foreach (var screen in Forms.Screen.AllScreens)
            {
                var window = new ChildLockWindow(screen.Bounds, imagePath, _options.ShowCountdown, startsBlack);
                window.Show();
                replacements.Add(window);
            }
        }
        catch
        {
            foreach (var replacement in replacements)
            {
                replacement.Close();
            }

            throw;
        }

        foreach (var window in _windows)
        {
            window.Close();
        }

        _windows.Clear();
        _windows.AddRange(replacements);
    }

    private void SuppressPostTimeoutCountdown()
    {
        if (!_isPostTimeoutProtection)
        {
            return;
        }

        foreach (var window in _windows)
        {
            window.EnterPersistentBlackMode();
        }
    }

    private void Unlock()
    {
        State = ChildLockState.Ended;
        AppLogService.Current.Info("Child lock session unlocked.");
        End(raiseEndedEvent: true);
    }

    private void CheckEmergencyRecovery()
    {
        if (!_foregroundAppService.IsTaskManagerForeground())
        {
            return;
        }

        State = ChildLockState.Ended;
        AppLogService.Current.Info("Task Manager became foreground; ending child lock for emergency recovery.");
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
        _powerService.Resumed -= OnSystemResumed;
        _powerService.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _powerService.Dispose();
        _recoveryService.ClearOwnedByCurrentProcess();
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
        _unlockHoldCompleted = false;
        _recoveryMonitorTimer.Stop();
        _sleepRecoveryTimer.Stop();
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
