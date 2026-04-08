using System.Net.NetworkInformation;
using Microsoft.VisualBasic.Devices;
using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

public sealed class SystemStatusService
{
    private readonly HardwareMonitorService _hardwareMonitorService;

    public SystemStatusService(HardwareMonitorService hardwareMonitorService)
    {
        _hardwareMonitorService = hardwareMonitorService;
    }

    public async Task<IReadOnlyList<PingHostResult>> PingDefaultHostsAsync(CancellationToken cancellationToken)
    {
        var hosts = new[] { "1.1.1.1", "8.8.8.8" };
        var results = new List<PingHostResult>(hosts.Length);

        using var ping = new Ping();
        foreach (var host in hosts)
        {
            try
            {
                var reply = await ping.SendPingAsync(host, 1500);
                results.Add(reply.Status == IPStatus.Success
                    ? new PingHostResult(host, reply.RoundtripTime, true, null)
                    : new PingHostResult(host, null, false, reply.Status.ToString()));
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                results.Add(new PingHostResult(host, null, false, exception.Message));
            }
        }

        return results;
    }

    public SystemStatusSnapshot Collect()
    {
        var computerInfo = new ComputerInfo();
        var drives = DriveInfo.GetDrives()
            .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
            .Select(drive => new DriveSnapshot(
                drive.Name,
                Math.Round(drive.TotalSize / 1024d / 1024d / 1024d, 1),
                Math.Round(drive.AvailableFreeSpace / 1024d / 1024d / 1024d, 1)))
            .ToList();

        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                              !System.Net.IPAddress.IsLoopback(address.Address))
            .Select(address => address.Address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SystemStatusSnapshot(
            MachineName: Environment.MachineName,
            OsDescription: $"{Environment.OSVersion.VersionString} ({Environment.Is64BitOperatingSystem switch { true => "x64", false => "x86" }})",
            Uptime: TimeSpan.FromMilliseconds(Environment.TickCount64),
            TotalMemoryMb: computerInfo.TotalPhysicalMemory / 1024 / 1024,
            AvailableMemoryMb: computerInfo.AvailablePhysicalMemory / 1024 / 1024,
            LocalIpv4Addresses: addresses,
            Drives: drives,
            Hardware: _hardwareMonitorService.Read());
    }
}
