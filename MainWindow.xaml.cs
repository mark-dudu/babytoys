using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using BabyToys.Models;
using BabyToys.Services;
using BabyToys.Sessions;
using Microsoft.Win32;

namespace BabyToys;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly DispatcherTimer _confirmTimer = new();
    private AppSettings _settings = new();
    private int _confirmRemainingSeconds;
    private ChildLockSession? _session;

    public MainWindow()
    {
        InitializeComponent();

        _confirmTimer.Interval = TimeSpan.FromSeconds(1);
        _confirmTimer.Tick += ConfirmTimer_Tick;

        Loaded += (_, _) => LoadSettings();
    }

    private void LoadSettings()
    {
        _settings = _settingsService.Load();
        SystemWallpaperRadio.IsChecked = _settings.ImageSourceMode == ImageSourceMode.SystemWallpaper;
        CustomImageRadio.IsChecked = _settings.ImageSourceMode == ImageSourceMode.CustomImage;
        CustomImagePathTextBox.Text = _settings.CustomImagePath ?? string.Empty;
        _settings.DurationMinutes = NormalizeDurationMinutes(_settings.DurationMinutes);
        DurationTextBox.Text = FormatDurationMinutes(_settings.DurationMinutes);
        ShowCountdownCheckBox.IsChecked = _settings.ShowCountdown;
        UpdateImageControls();
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
        _settingsService.Save(_settings);
    }

    private double ReadDurationMinutes()
    {
        var text = DurationTextBox.Text.Trim();
        if (!TryParseDurationMinutes(text, out var minutes))
        {
            StatusTextBlock.Text = "时间设置无效，已恢复为 5 分钟";
            DurationTextBox.Text = "5";
            return 5;
        }

        var normalized = NormalizeDurationMinutes(minutes);
        if (Math.Abs(normalized - minutes) > 0.0001)
        {
            StatusTextBlock.Text = $"时间已调整为 {FormatDurationMinutes(normalized)} 分钟";
            DurationTextBox.Text = FormatDurationMinutes(normalized);
        }

        return normalized;
    }

    private static bool TryParseDurationMinutes(string text, out double minutes)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out minutes) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out minutes);
    }

    private static double NormalizeDurationMinutes(double minutes)
    {
        if (double.IsNaN(minutes) || double.IsInfinity(minutes))
        {
            return 5;
        }

        return Math.Clamp(minutes, 0.5, 240);
    }

    private static string FormatDurationMinutes(double minutes)
    {
        return minutes.ToString("0.##", CultureInfo.InvariantCulture);
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

    private void BeginConfirmCountdown()
    {
        _confirmRemainingSeconds = 3;
        StartButton.IsEnabled = false;
        CancelCountdownButton.Visibility = Visibility.Visible;
        StatusTextBlock.Text = "3 秒后进入儿童模式，可取消";
        _confirmTimer.Start();
    }

    private void CancelCountdownButton_Click(object sender, RoutedEventArgs e)
    {
        CancelConfirmCountdown("已取消");
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

        StatusTextBlock.Text = $"{_confirmRemainingSeconds} 秒后进入儿童模式，可取消";
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
        _confirmTimer.Stop();
        _session?.Dispose();
        SaveSettingsFromUi();
        base.OnClosing(e);
    }
}
