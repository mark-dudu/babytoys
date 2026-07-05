using System.Runtime.InteropServices;
using Drawing = System.Drawing;

namespace BabyToys.Services;

public static class WindowPlacementService
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpShowWindow = 0x0040;

    public static void PlaceTopmost(IntPtr handle, Drawing.Rectangle bounds)
    {
        SetWindowPos(handle, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpShowWindow);
    }

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
