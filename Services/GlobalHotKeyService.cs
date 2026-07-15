using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace BabyToys.Services;

public sealed class GlobalHotKeyService : IDisposable
{
    private const int HotKeyId = 0x4254;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkB = 0x42;

    private HwndSource? _source;
    private IntPtr _handle;
    private bool _registered;

    public event EventHandler? Pressed;

    public bool Register(System.Windows.Window window)
    {
        Unregister();
        _handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
        _registered = RegisterHotKey(_handle, HotKeyId, ModControl | ModAlt, VkB);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_handle, HotKeyId);
        }

        _source?.RemoveHook(WndProc);
        _source = null;
        _handle = IntPtr.Zero;
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
