namespace TgPcAgent.App.Scripts.Tools;

public sealed record TgSendFileParams(string Path);

public sealed class TgSendFileTool : ScriptToolBase<TgSendFileParams>
{
    public override string Name => "tg.sendFile";
    public override string Description => "Отправить файл в Telegram (макс 50MB)";

    protected override async Task<StepResult> ExecuteTypedAsync(TgSendFileParams p, ScriptContext ctx, CancellationToken ct)
    {
        if (!File.Exists(p.Path))
            return new StepResult(false, Error: "Файл не найден");

        var fi = new FileInfo(p.Path);
        if (fi.Length > 50 * 1024 * 1024)
            return new StepResult(false, Error: "Файл > 50MB");

        var bytes = await File.ReadAllBytesAsync(p.Path, ct);
        await ctx.Sender.SendDocumentAsync(ctx.ChatId, bytes, fi.Name, "", ct);
        return new StepResult(true, $"Отправлен ({fi.Length / 1024.0:F0} KB)");
    }
}
