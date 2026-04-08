using System.Net;
using System.Net.NetworkInformation;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record SysInfoParams;

public sealed class SysInfoTool : ScriptToolBase<SysInfoParams>
{
    public override string Name => "sys.info";
    public override string Description => "Информация о системе";

    protected override Task<StepResult> ExecuteTypedAsync(SysInfoParams p, ScriptContext ctx, CancellationToken ct)
    {
        var lines = new List<string>
        {
            $"Имя ПК: {Environment.MachineName}",
            $"Пользователь: {Environment.UserName}",
            $"ОС: {Environment.OSVersion}",
            $"64-bit: {Environment.Is64BitOperatingSystem}",
            $"Процессоры: {Environment.ProcessorCount}",
            $"Аптайм: {TimeSpan.FromMilliseconds(Environment.TickCount64):d\\.hh\\:mm\\:ss}",
            $".NET: {Environment.Version}"
        };

        return Task.FromResult(new StepResult(true, string.Join("\n", lines)));
    }
}
