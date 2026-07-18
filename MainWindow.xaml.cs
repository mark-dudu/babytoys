using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BabyToys.Models;
using BabyToys.Services;
using BabyToys.Sessions;
using BabyToys.Views;
using Microsoft.Win32;

namespace BabyToys;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly StartupService _startupService = new();
    private readonly GlobalHotKeyService _globalHotKeyService = new();
    private readonly TrayIconService _trayIconService = new();
    private readonly SessionRecoveryService _recoveryService = new();
    private readonly DispatcherTimer _confirmTimer = new();
    private AppSettings _settings = new();
    private int _confirmRemainingSeconds;
    private ChildLockSession? _session;
    private bool _allowClose;
    private bool _loadingPreset;

    public MainWindow()
    {
        InitializeComponent();
        Icon = AppIconService.WindowIcon;

        _confirmTimer.Interval = TimeSpan.FromSeconds(1);
        _confirmTimer.Tick += ConfirmTimer_Tick;

        _trayIconService.ShowRequested += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        _trayIconService.StartRequested += (_, _) => Dispatcher.Invoke(StartFromShortcut);
        _trayIconService.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);
        _globalHotKeyService.Pressed += (_, _) => StartFromShortcut();

        Loaded += (_, _) =>
        {
            LoadSettings();
            CheckPreviousSessionRecovery();
            UpdateGlobalHotKeyRegistration();
            var minimizedLaunch = Environment.GetCommandLineArgs()
                .Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            if (minimizedLaunch && !_settings.StartWithWindows)
            {
                AppLogService.Current.Info("Ignoring stale minimized launch because startup is disabled.");
                _allowClose = true;
                Close();
                return;
            }

            if (minimizedLaunch)
            {
                Hide();
            }
        };
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void LoadSettings()
    {
        _settings = _settingsService.Load();
        SystemWallpaperRadio.IsChecked = _settings.ImageSourceMode == ImageSourceMode.SystemWallpaper;
        CustomImageRadio.IsChecked = _settings.ImageSourceMode == ImageSourceMode.CustomImage;
        CustomImagePathTextBox.Text = _settings.CustomImagePath ?? string.Empty;
        DurationTextBox.Text = SettingsPolicy.FormatDurationMinutes(_settings.DurationMinutes);
        ShowCountdownCheckBox.IsChecked = _settings.ShowCountdown;
        EnableGlobalHotKeyCheckBox.IsChecked = _settings.EnableGlobalHotKey;
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        ReconcileStartupRegistration();
        RefreshPresetList(_settings.SelectedPresetName);
        UpdateImageControls();
        if (_settingsService.IsReadOnlyDueToUnsupportedSchema)
        {
            StatusTextBlock.Text = $"检测到来自更高版本的配置（版本 {_settingsService.UnsupportedSchemaVersion}），本次更改不会保存";
        }
    }

    private void SaveSettingsFromUi()
    {
        _settings.ImageSourceMode = CustomImageRadio.IsChecked == true
            ? ImageSourceMode.CustomImage
            : ImageSourceMode.SystemWallpaper;
        _settings.CustomImagePath = string.IsNullOrWhiteSpace(CustomImagePathTextBox.Text)
            ? null
            : CustomImagePathTextBox.Text;
        _settings.DurationMinutes = ReadDurationMinutes();
        _settings.ShowCountdown = ShowCountdownCheckBox.IsChecked == true;
        _settings.EnableGlobalHotKey = EnableGlobalHotKeyCheckBox.IsChecked == true;
        var startWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        if (_settingsService.IsReadOnlyDueToUnsupportedSchema)
        {
            startWithWindows = _settings.StartWithWindows;
            StartWithWindowsCheckBox.IsChecked = startWithWindows;
        }
        else if (startWithWindows != _startupService.IsEnabled() && !_startupService.TrySetEnabled(startWithWindows))
        {
            startWithWindows = _settings.StartWithWindows;
            StartWithWindowsCheckBox.IsChecked = startWithWindows;
            StatusTextBlock.Text = "无法更新开机自启设置";
        }

        _settings.StartWithWindows = startWithWindows;
        _settingsService.Save(_settings);
        UpdateGlobalHotKeyRegistration();
    }

    private void ReconcileStartupRegistration()
    {
        if (_settingsService.IsReadOnlyDueToUnsupportedSchema)
        {
            return;
        }

        if (_settings.StartWithWindows == _startupService.IsEnabled())
        {
            return;
        }

        if (!_startupService.TrySetEnabled(_settings.StartWithWindows))
        {
            StatusTextBlock.Text = "开机自启状态与系统不一致，自动修复失败";
            return;
        }

        AppLogService.Current.Info("Startup registration reconciled with saved settings.");
    }

    private double ReadDurationMinutes()
    {
        var text = DurationTextBox.Text.Trim();
        if (!SettingsPolicy.TryParseDurationMinutes(text, out var minutes))
        {
            StatusTextBlock.Text = "时间设置无效，已恢复为 5 分钟";
            DurationTextBox.Text = "5";
            return 5;
        }

        var normalized = SettingsPolicy.NormalizeDurationMinutes(minutes);
        if (Math.Abs(normalized - minutes) > 0.0001)
        {
            StatusTextBlock.Text = $"时间已调整为 {SettingsPolicy.FormatDurationMinutes(normalized)} 分钟";
            DurationTextBox.Text = SettingsPolicy.FormatDurationMinutes(normalized);
        }

        return normalized;
    }

    private void ImageSource_Checked(object sender, RoutedEventArgs e)
    {
        UpdateImageControls();
    }

    private void UpdateImageControls()
    {
        if (ChooseImageButton is null || CustomImagePathTextBox is null)
        {
            return;
        }

        var customEnabled = CustomImageRadio?.IsChecked == true;
        ChooseImageButton.IsEnabled = customEnabled;
        CustomImagePathTextBox.IsEnabled = customEnabled;
    }

    private void ChooseImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择儿童模式图片",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            CustomImagePathTextBox.Text = dialog.FileName;
            SaveSettingsFromUi();
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        BeginConfirmCountdown();
    }

    private void CheckPreviousSessionRecovery()
    {
        var marker = _recoveryService.ReadPreviousMarker();
        if (marker is null)
        {
            return;
        }

        if (_recoveryService.IsMarkerProcessActive(marker))
        {
            AppLogService.Current.Info($"Another BabyToys process owns the active session marker. Process id: {marker.ProcessId}.");
            StatusTextBlock.Text = "检测到另一个 BabyToys 实例正在运行儿童模式";
            return;
        }

        AppLogService.Current.Error($"Previous child lock session ended unexpectedly. Started at {marker.StartedAt:O}.");
        _recoveryService.Clear();
        StatusTextBlock.Text = "检测到上次儿童模式异常结束，输入拦截已随旧进程释放；可打开诊断查看日志";
    }

    private void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new DiagnosticsWindow { Owner = this };
        window.ShowDialog();
    }

    private void StartFromShortcut()
    {
        if (_session is not null || _confirmTimer.IsEnabled)
        {
            ShowMainWindow();
            StatusTextBlock.Text = "儿童模式已在启动或运行中";
            return;
        }

        ShowMainWindow();
        SaveSettingsFromUi();
        BeginConfirmCountdown();
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void UpdateGlobalHotKeyRegistration()
    {
        _globalHotKeyService.Unregister();
        if (_settings.EnableGlobalHotKey && !_globalHotKeyService.Register(this))
        {
            StatusTextBlock.Text = "快捷键 Ctrl + Alt + B 已被其他程序占用";
        }
    }

    private void RefreshPresetList(string? selectedName)
    {
        _loadingPreset = true;
        PresetComboBox.ItemsSource = null;
        PresetComboBox.ItemsSource = _settings.Presets.Select(preset => preset.Name).ToList();
        PresetComboBox.Text = selectedName ?? string.Empty;
        _loadingPreset = false;
    }

    private void PresetComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingPreset || PresetComboBox.SelectedItem is not string name)
        {
            return;
        }

        var preset = _settings.Presets.FirstOrDefault(item => item.Name == name);
        if (preset is null)
        {
            return;
        }

        SystemWallpaperRadio.IsChecked = preset.ImageSourceMode == ImageSourceMode.SystemWallpaper;
        CustomImageRadio.IsChecked = preset.ImageSourceMode == ImageSourceMode.CustomImage;
        CustomImagePathTextBox.Text = preset.CustomImagePath ?? string.Empty;
        DurationTextBox.Text = SettingsPolicy.FormatDurationMinutes(preset.DurationMinutes);
        ShowCountdownCheckBox.IsChecked = preset.ShowCountdown;
        _settings.SelectedPresetName = preset.Name;
        _settingsService.Save(_settings);
        StatusTextBlock.Text = $"已应用预设：{preset.Name}";
    }

    private void SavePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetComboBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusTextBlock.Text = "请先输入预设名称";
            PresetComboBox.Focus();
            return;
        }

        SaveSettingsFromUi();
        var preset = _settings.Presets.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            preset = new ChildModePreset { Name = name };
            _settings.Presets.Add(preset);
        }

        preset.Name = name;
        preset.ImageSourceMode = _settings.ImageSourceMode;
        preset.CustomImagePath = _settings.CustomImagePath;
        preset.DurationMinutes = _settings.DurationMinutes;
        preset.ShowCountdown = _settings.ShowCountdown;
        _settings.SelectedPresetName = name;
        _settingsService.Save(_settings);
        RefreshPresetList(name);
        StatusTextBlock.Text = $"已保存预设：{name}";
    }

    private void DeletePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetComboBox.Text.Trim();
        var removed = _settings.Presets.RemoveAll(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            StatusTextBlock.Text = "没有可删除的预设";
            return;
        }

        _settings.SelectedPresetName = null;
        _settingsService.Save(_settings);
        RefreshPresetList(null);
        StatusTextBlock.Text = $"已删除预设：{name}";
    }

    private void OptionCheckBox_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
    }

    private void BeginConfirmCountdown()
    {
        _confirmRemainingSeconds = 3;
        StartButton.IsEnabled = false;
        CancelCountdownButton.Visibility = Visibility.Visible;
        StatusTextBlock.Text = "3 秒后进入儿童模式，按 Esc 可取消";
        _confirmTimer.Start();
    }

    private void CancelCountdownButton_Click(object sender, RoutedEventArgs e)
    {
        CancelConfirmCountdown("已取消");
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _confirmTimer.IsEnabled)
        {
            CancelConfirmCountdown("已取消");
            e.Handled = true;
        }
    }

    private void ConfirmTimer_Tick(object? sender, EventArgs e)
    {
        _confirmRemainingSeconds--;
        if (_confirmRemainingSeconds <= 0)
        {
            _confirmTimer.Stop();
            CancelCountdownButton.Visibility = Visibility.Collapsed;
            StartChildLockSession();
            return;
        }

        StatusTextBlock.Text = $"{_confirmRemainingSeconds} 秒后进入儿童模式，按 Esc 可取消";
    }

    private void CancelConfirmCountdown(string status)
    {
        _confirmTimer.Stop();
        StartButton.IsEnabled = true;
        CancelCountdownButton.Visibility = Visibility.Collapsed;
        StatusTextBlock.Text = status;
    }

    private void StartChildLockSession()
    {
        var options = new ChildLockOptions
        {
            ImageSourceMode = _settings.ImageSourceMode,
            CustomImagePath = _settings.CustomImagePath,
            Duration = TimeSpan.FromMinutes(_settings.DurationMinutes),
            ShowCountdown = _settings.ShowCountdown
        };

        _session = new ChildLockSession(
            options,
            new InputHookService(),
            new WallpaperService(),
            new PowerService());
        _session.Ended += OnSessionEnded;

        if (_session.Start(this))
        {
            Hide();
            StatusTextBlock.Text = "儿童模式运行中";
            AppLogService.Current.Info("Main window hidden while child lock runs.");
        }
        else
        {
            _session.Dispose();
            _session = null;
            StartButton.IsEnabled = true;
            StatusTextBlock.Text = "无法进入儿童模式";
        }
    }

    private void OnSessionEnded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _session?.Dispose();
            _session = null;
            Show();
            Activate();
            StartButton.IsEnabled = true;
            StatusTextBlock.Text = "儿童模式已结束";
        });
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            SaveSettingsFromUi();
            e.Cancel = true;
            Hide();
            return;
        }

        _confirmTimer.Stop();
        _session?.Dispose();
        SaveSettingsFromUi();
        _globalHotKeyService.Dispose();
        _trayIconService.Dispose();
        base.OnClosing(e);
    }

    private void ExitApplication()
    {
        _allowClose = true;
        Close();
    }
}
