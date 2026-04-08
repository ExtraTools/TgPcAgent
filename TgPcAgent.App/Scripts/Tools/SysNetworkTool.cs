using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record SysNetworkParams;

public sealed class SysNetworkTool : ScriptToolBase<SysNetworkParams>
{
    public override string Name => "sys.network";
    public override string Description => "Сетевая информация";

    protected override Task<StepResult> ExecuteTypedAsync(SysNetworkParams p, ScriptContext ctx, CancellationToken ct)
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(ni =>
            {
                var ips = ni.GetIPProperties().UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString());
                return $"{ni.Name}: {string.Join(", ", ips)} ({ni.Speed / 1_000_000} Mbps)";
            })
            .ToList();

        if (interfaces.Count == 0)
            return Task.FromResult(new StepResult(true, "Нет активных интерфейсов"));

        return Task.FromResult(new StepResult(true, string.Join("\n", interfaces)));
    }
}
