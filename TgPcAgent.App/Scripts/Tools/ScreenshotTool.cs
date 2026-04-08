namespace TgPcAgent.App.Scripts.Tools;

public sealed record ScreenshotParams;

public sealed class ScreenshotTool : ScriptToolBase<ScreenshotParams>
{
    public override string Name => "screenshot";
    public override string Description => "Сделать скриншот экрана";

    protected override async Task<StepResult> ExecuteTypedAsync(ScreenshotParams p, ScriptContext ctx, CancellationToken ct)
    {
        var capture = ctx.Screenshots.CaptureVirtualScreen();
        if (capture.Content == null || capture.Content.Length == 0)
            return new StepResult(false, Error: "Не удалось сделать скриншот");

        await ctx.Sender.SendPhotoAsync(ctx.ChatId, capture.Content, "screenshot.jpg", "<b>📸 Скриншот</b>", ct);
        return new StepResult(true, "Скриншот отправлен");
    }
}
