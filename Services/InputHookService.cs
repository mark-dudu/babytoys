using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BabyToys.Services;

public sealed class InputHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkU = 0x55;

    private readonly LowLevelHookProc _keyboardProc;
    private readonly LowLevelHookProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _isCtrlDown;
    private bool _isAltDown;
    private bool _isUDown;
    private bool _isUnlockComboDown;
    private bool _disposed;

    public event EventHandler<bool>? UnlockComboStateChanged;

    public bool IsInstalled => _keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero;

    public InputHookService()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public bool TryInstall()
    {
        if (IsInstalled)
        {
            return true;
        }

        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            var currentModule = currentProcess.MainModule;
            var moduleHandle = currentModule is null ? IntPtr.Zero : GetModuleHandle(currentModule.ModuleName);

            _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, moduleHandle, 0);
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, moduleHandle, 0);

            if (!IsInstalled)
            {
                Uninstall();
                AppLogService.Current.Error("Failed to install input hooks.");
                return false;
            }

            AppLogService.Current.Info("Input hooks installed.");
            return true;
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to install input hooks.", ex);
            Uninstall();
            return false;
        }
    }

    public void Uninstall()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        _isCtrlDown = false;
        _isAltDown = false;
        _isUDown = false;
        SetUnlockComboDown(false);
        AppLogService.Current.Info("Input hooks uninstalled.");
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp)
            {
                var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                var isDown = message is WmKeyDown or WmSysKeyDown;
                UpdateUnlockState(data.VirtualKeyCode, isDown);
            }

            return new IntPtr(1);
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            return new IntPtr(1);
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void UpdateUnlockState(int virtualKeyCode, bool isDown)
    {
        switch (virtualKeyCode)
        {
            case VkControl:
            case 0xA2:
            case 0xA3:
                _isCtrlDown = isDown;
                break;
            case VkMenu:
            case 0xA4:
            case 0xA5:
                _isAltDown = isDown;
                break;
            case VkU:
                _isUDown = isDown;
                break;
        }

        SetUnlockComboDown(_isCtrlDown && _isAltDown && _isUDown);
    }

    private void SetUnlockComboDown(bool isDown)
    {
        if (_isUnlockComboDown == isDown)
        {
            return;
        }

        _isUnlockComboDown = isDown;
        UnlockComboStateChanged?.Invoke(this, isDown);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Uninstall();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private delegate IntPtr LowLevelHookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KbdLlHookStruct
    {
        public readonly int VirtualKeyCode;
        public readonly int ScanCode;
        public readonly int Flags;
        public readonly int Time;
        public readonly IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelHookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
