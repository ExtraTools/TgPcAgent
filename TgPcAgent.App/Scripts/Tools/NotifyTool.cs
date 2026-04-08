namespace TgPcAgent.App.Scripts.Tools;

public sealed record NotifyParams(string Text);

public sealed class NotifyTool : ScriptToolBase<NotifyParams>
{
    public override string Name => "notify";
    public override string Description => "Отправить сообщение в Telegram";

    protected override async Task<StepResult> ExecuteTypedAsync(NotifyParams p, ScriptContext ctx, CancellationToken ct)
    {
        await ctx.Sender.SendTextAsync(ctx.ChatId, p.Text, null, ct);
        return new StepResult(true, "Отправлено");
    }
}
