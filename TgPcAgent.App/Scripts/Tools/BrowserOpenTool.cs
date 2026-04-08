using System.Diagnostics;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record BrowserOpenParams(string Url);

public sealed class BrowserOpenTool : ScriptToolBase<BrowserOpenParams>
{
    public override string Name => "browser.open";
    public override string Description => "Открыть URL в браузере";

    protected override Task<StepResult> ExecuteTypedAsync(BrowserOpenParams p, ScriptContext ctx, CancellationToken ct)
    {
        Process.Start(new ProcessStartInfo(p.Url) { UseShellExecute = true });
        return Task.FromResult(new StepResult(true, $"Открыт: {p.Url}"));
    }
}
