namespace TgPcAgent.App.Models;

public sealed record ScreenshotCapture(
    byte[] Content,
    int Width,
    int Height);
