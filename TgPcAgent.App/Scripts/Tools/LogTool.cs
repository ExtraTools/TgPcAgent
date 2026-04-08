namespace TgPcAgent.App.Scripts.Tools;

public sealed record LogParams(string Text);

public sealed class LogTool : ScriptToolBase<LogParams>
{
    public override string Name => "log";
    public override string Description => "Записать в лог агента";

    protected override Task<StepResult> ExecuteTypedAsync(LogParams p, ScriptContext ctx, CancellationToken ct)
    {
        ctx.Logger.Info($"[Script] {p.Text}");
        return Task.FromResult(new StepResult(true, "Записано в лог"));
    }
}
