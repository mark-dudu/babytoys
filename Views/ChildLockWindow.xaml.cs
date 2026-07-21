using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using BabyToys.Services;
using Drawing = System.Drawing;

namespace BabyToys.Views;

public partial class ChildLockWindow : Window
{
    private readonly bool _showCountdown;
    private readonly Drawing.Rectangle _bounds;
    private bool _countdownSuppressed;

    public ChildLockWindow(Drawing.Rectangle bounds, string? imagePath, bool showCountdown, bool startsBlack)
    {
        InitializeComponent();
        _showCountdown = showCountdown;
        _bounds = bounds;

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        SourceInitialized += OnSourceInitialized;

        StatusPanel.Visibility = showCountdown ? Visibility.Visible : Visibility.Collapsed;
        RemainingText.Visibility = showCountdown ? Visibility.Visible : Visibility.Collapsed;
        LoadImage(imagePath);
        if (startsBlack)
        {
            ShowBlackImmediately();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        WindowPlacementService.PlaceTopmost(handle, _bounds);
    }

    public void FadeToBlack()
    {
        var animation = new DoubleAnimation(1, TimeSpan.FromMilliseconds(800));
        BlackOverlay.BeginAnimation(OpacityProperty, animation);
    }

    public void ShowBlackImmediately()
    {
        BlackOverlay.BeginAnimation(OpacityProperty, null);
        BlackOverlay.Opacity = 1;
    }

    public void EnterPersistentBlackMode()
    {
        _countdownSuppressed = true;
        ShowBlackImmediately();
        RemainingText.Visibility = Visibility.Collapsed;
        if (UnlockText.Visibility != Visibility.Visible)
        {
            StatusPanel.Visibility = Visibility.Collapsed;
        }
    }

    public void SetRemaining(TimeSpan remaining)
    {
        if (!_showCountdown)
        {
            return;
        }

        RemainingText.Text = remaining.TotalHours >= 1
            ? $"剩余 {(int)remaining.TotalHours:0}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"剩余 {remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    public void SetUnlockProgress(TimeSpan elapsed, bool isReady)
    {
        StatusPanel.Visibility = Visibility.Visible;
        UnlockText.Visibility = Visibility.Visible;
        UnlockProgressBar.Visibility = Visibility.Visible;
        UnlockProgressBar.Value = Math.Clamp(elapsed.TotalSeconds, 0, 3);
        UnlockText.Text = isReady
            ? "已完成，松开即可解锁"
            : $"继续按住 {Math.Max(0, 3 - elapsed.TotalSeconds):0.0} 秒";
    }

    public void HideUnlockProgress()
    {
        UnlockText.Visibility = Visibility.Collapsed;
        UnlockProgressBar.Visibility = Visibility.Collapsed;
        StatusPanel.Visibility = _showCountdown && !_countdownSuppressed
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LoadImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            DisplayImage.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath!, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            DisplayImage.Source = bitmap;
        }
        catch
        {
            DisplayImage.Visibility = Visibility.Collapsed;
        }
    }

}
