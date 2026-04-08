namespace TgPcAgent.App.Scripts.Tools;

public sealed record SysDrivesParams;

public sealed class SysDrivesTool : ScriptToolBase<SysDrivesParams>
{
    public override string Name => "sys.drives";
    public override string Description => "Информация о дисках";

    protected override Task<StepResult> ExecuteTypedAsync(SysDrivesParams p, ScriptContext ctx, CancellationToken ct)
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => $"{d.Name} — {d.AvailableFreeSpace / (1024.0 * 1024 * 1024):F1}/{d.TotalSize / (1024.0 * 1024 * 1024):F1} GB свободно ({d.DriveFormat})")
            .ToList();

        return Task.FromResult(new StepResult(true, string.Join("\n", drives)));
    }
}
