namespace TgPcAgent.App.Scripts.Tools;

public sealed record WaitParams(int Ms);

public sealed class WaitTool : ScriptToolBase<WaitParams>
{
    public override string Name => "wait";
    public override string Description => "Пауза (макс 30000мс)";

    protected override async Task<StepResult> ExecuteTypedAsync(WaitParams p, ScriptContext ctx, CancellationToken ct)
    {
        var ms = Math.Clamp(p.Ms, 0, 30000);
        await Task.Delay(ms, ct);
        return new StepResult(true, $"Ожидание {ms}мс");
    }
}
