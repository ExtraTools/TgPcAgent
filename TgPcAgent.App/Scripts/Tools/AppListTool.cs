using System.Diagnostics;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record AppListParams(int? Top = 10);

public sealed class AppListTool : ScriptToolBase<AppListParams>
{
    public override string Name => "app.list";
    public override string Description => "Список запущенных процессов (топ по RAM)";

    protected override Task<StepResult> ExecuteTypedAsync(AppListParams p, ScriptContext ctx, CancellationToken ct)
    {
        var top = Math.Clamp(p.Top ?? 10, 1, 30);
        var procs = Process.GetProcesses()
            .OrderByDescending(proc => { try { return proc.WorkingSet64; } catch { return 0L; } })
            .Take(top)
            .Select(proc =>
            {
                try { return $"{proc.ProcessName} (PID {proc.Id}) — {proc.WorkingSet64 / (1024.0 * 1024):F0} MB"; }
                catch { return $"{proc.ProcessName} (PID {proc.Id})"; }
            })
            .ToList();

        return Task.FromResult(new StepResult(true, string.Join("\n", procs)));
    }
}
