using System.Drawing.Imaging;

using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

public sealed class ScreenshotService
{
    public ScreenshotCapture CaptureVirtualScreen()
    {
        var bounds = SystemInformation.VirtualScreen;
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);

        using var stream = new MemoryStream();
        // Используем встроенный Jpeg для скорости и меньшего веса
        bitmap.Save(stream, ImageFormat.Jpeg);
        return new ScreenshotCapture(stream.ToArray(), bounds.Width, bounds.Height);
    }
}
