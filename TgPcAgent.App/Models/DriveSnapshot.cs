namespace TgPcAgent.App.Models;

public sealed record DriveSnapshot(
    string Name,
    double TotalGb,
    double FreeGb);
