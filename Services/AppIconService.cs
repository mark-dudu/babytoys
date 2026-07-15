using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace BabyToys.Services;

public static class AppIconService
{
    private static readonly Uri IconResourceUri = new(
        "pack://application:,,,/Assets/AppIcon/BabyToys.ico",
        UriKind.Absolute);

    public static ImageSource WindowIcon { get; } = LoadWindowIcon();

    public static Drawing.Icon CreateTrayIcon()
    {
        using var stream = OpenIconStream();
        using var icon = new Drawing.Icon(stream);
        return (Drawing.Icon)icon.Clone();
    }

    private static ImageSource LoadWindowIcon()
    {
        using var stream = OpenIconStream();
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.MaxBy(candidate => candidate.PixelWidth * candidate.PixelHeight)
            ?? throw new InvalidOperationException("应用图标不包含可用图像。");
        frame.Freeze();
        return frame;
    }

    private static Stream OpenIconStream()
    {
        var resource = System.Windows.Application.GetResourceStream(IconResourceUri)
            ?? throw new InvalidOperationException("找不到内嵌的应用图标资源。");
        return resource.Stream;
    }
}
