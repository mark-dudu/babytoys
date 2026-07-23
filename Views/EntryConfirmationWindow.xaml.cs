using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using BabyToys.Services;
using Drawing = System.Drawing;

namespace BabyToys.Views;

public partial class EntryConfirmationWindow : Window
{
    private readonly Drawing.Rectangle _bounds;
    private bool _allowClose;

    public bool IsPrimary { get; }

    public event EventHandler? CancelRequested;

    public EntryConfirmationWindow(Drawing.Rectangle bounds, bool isPrimary)
    {
        InitializeComponent();
        _bounds = bounds;
        IsPrimary = isPrimary;

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        ShowActivated = isPrimary;
        Cursor = isPrimary
            ? System.Windows.Input.Cursors.Arrow
            : System.Windows.Input.Cursors.None;
        ConfirmationPanel.Visibility = isPrimary ? Visibility.Visible : Visibility.Collapsed;
        SourceInitialized += OnSourceInitialized;
    }

    public void SetRemainingSeconds(int seconds)
    {
        CountdownText.Text = $"{seconds} 秒后进入儿童模式";
    }

    public void ActivateCancellationControls()
    {
        if (!IsPrimary)
        {
            return;
        }

        Activate();
        CancelButton.Focus();
    }

    public void CloseForCleanup()
    {
        _allowClose = true;
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        WindowPlacementService.PlaceTopmost(handle, _bounds);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CancelRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Dispatcher.BeginInvoke(
            () => CancelRequested?.Invoke(this, EventArgs.Empty));
    }
}
