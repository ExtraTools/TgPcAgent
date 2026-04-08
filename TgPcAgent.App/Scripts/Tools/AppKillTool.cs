using System.Diagnostics;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record AppKillParams(string? Name = null, int? Pid = null);

public sealed class AppKillTool : ScriptToolBase<AppKillParams>
{
    public override string Name => "app.kill";
    public override string Description => "Убить процесс по имени или PID";

    protected override Task<StepResult> ExecuteTypedAsync(AppKillParams p, ScriptContext ctx, CancellationToken ct)
    {
        if (p.Pid.HasValue)
        {
            var proc = Process.GetProcessById(p.Pid.Value);
            proc.Kill(true);
            return Task.FromResult(new StepResult(true, $"Убит PID {p.Pid}"));
        }

        if (!string.IsNullOrWhiteSpace(p.Name))
        {
            var name = p.Name.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
            var procs = Process.GetProcessesByName(name);
            if (procs.Length == 0)
                return Task.FromResult(new StepResult(false, Error: $"Процесс '{p.Name}' не найден"));

            foreach (var proc in procs)
            {
                try { proc.Kill(true); } catch { }
                proc.Dispose();
            }
            return Task.FromResult(new StepResult(true, $"Убито {procs.Length} процессов '{name}'"));
        }

        return Task.FromResult(new StepResult(false, Error: "Укажи name или pid"));
    }
}
