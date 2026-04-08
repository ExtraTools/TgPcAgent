namespace TgPcAgent.App.Models;

public sealed record SystemStatusSnapshot(
    string MachineName,
    string OsDescription,
    TimeSpan Uptime,
    ulong TotalMemoryMb,
    ulong AvailableMemoryMb,
    IReadOnlyList<string> LocalIpv4Addresses,
    IReadOnlyList<DriveSnapshot> Drives,
    HardwareReadings Hardware);
