using System.Runtime.InteropServices;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record PowerLockParams;
public sealed record PowerSleepParams;
public sealed record PowerShutdownParams;
public sealed record PowerRestartParams;

public sealed class PowerLockTool : ScriptToolBase<PowerLockParams>
{
    public override string Name => "power.lock";
    public override string Description => "Заблокировать ПК";

    [DllImport("user32.dll")] private static extern bool LockWorkStation();

    protected override Task<StepResult> ExecuteTypedAsync(PowerLockParams p, ScriptContext ctx, CancellationToken ct)
    {
        LockWorkStation();
        return Task.FromResult(new StepResult(true, "ПК заблокирован"));
    }
}

public sealed class PowerSleepTool : ScriptToolBase<PowerSleepParams>
{
    public override string Name => "power.sleep";
    public override string Description => "Спящий режим";

    protected override Task<StepResult> ExecuteTypedAsync(PowerSleepParams p, ScriptContext ctx, CancellationToken ct)
    {
        Application.SetSuspendState(PowerState.Suspend, false, false);
        return Task.FromResult(new StepResult(true, "Спящий режим"));
    }
}

public sealed class PowerShutdownTool : ScriptToolBase<PowerShutdownParams>
{
    public override string Name => "power.shutdown";
    public override string Description => "Выключить ПК (мгновенно, БЕЗ подтверждения)";

    protected override Task<StepResult> ExecuteTypedAsync(PowerShutdownParams p, ScriptContext ctx, CancellationToken ct)
    {
        System.Diagnostics.Process.Start("shutdown", "/s /t 0");
        return Task.FromResult(new StepResult(true, "Выключение..."));
    }
}

public sealed class PowerRestartTool : ScriptToolBase<PowerRestartParams>
{
    public override string Name => "power.restart";
    public override string Description => "Перезагрузить ПК (мгновенно, БЕЗ подтверждения)";

    protected override Task<StepResult> ExecuteTypedAsync(PowerRestartParams p, ScriptContext ctx, CancellationToken ct)
    {
        System.Diagnostics.Process.Start("shutdown", "/r /t 0");
        return Task.FromResult(new StepResult(true, "Перезагрузка..."));
    }
}
