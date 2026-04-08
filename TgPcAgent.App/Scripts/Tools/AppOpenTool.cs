using System.Diagnostics;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record AppOpenParams(string Path, string? Args = null);

public sealed class AppOpenTool : ScriptToolBase<AppOpenParams>
{
    public override string Name => "app.open";
    public override string Description => "Запустить приложение (exe, URL-протокол, или имя из PATH)";

    protected override Task<StepResult> ExecuteTypedAsync(AppOpenParams p, ScriptContext ctx, CancellationToken ct)
    {
        var target = p.Path.Trim();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = target,
                Arguments = p.Args ?? "",
                UseShellExecute = true
            };
            Process.Start(psi);
            return Task.FromResult(new StepResult(true, $"Запущено: {target}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new StepResult(false, Error: $"Не удалось запустить '{target}': {ex.Message}"));
        }
    }
}
