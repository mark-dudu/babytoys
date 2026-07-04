using System.Runtime.InteropServices;
using System.Text;

namespace BabyToys.Services;

public sealed class WallpaperService
{
    private const uint SpiGetDeskWallpaper = 0x0073;
    private const int MaxPath = 260;

    public string? GetCurrentWallpaperPath()
    {
        try
        {
            var buffer = new StringBuilder(MaxPath);
            if (SystemParametersInfo(SpiGetDeskWallpaper, (uint)buffer.Capacity, buffer, 0))
            {
                var path = buffer.ToString();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return path;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogService.Current.Error("Failed to read wallpaper path.", ex);
        }

        return null;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(uint action, uint param, StringBuilder value, uint flags);
}
