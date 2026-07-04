using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using Drawing = System.Drawing;

namespace BabyToys.Views;

public partial class ChildLockWindow : Window
{
    private readonly bool _showCountdown;
    private readonly Drawing.Rectangle _bounds;

    public ChildLockWindow(Drawing.Rectangle bounds, string? imagePath, bool showCountdown)
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
        LoadImage(imagePath);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowPos(handle, HwndTopmost, _bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height, SwpShowWindow);
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

    public void SetUnlockProgress(bool isUnlocking)
    {
        UnlockText.Visibility = isUnlocking ? Visibility.Visible : Visibility.Collapsed;
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

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr handle,
        IntPtr insertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
