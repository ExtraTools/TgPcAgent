namespace TgPcAgent.App.Models;

public sealed record ProcessSnapshot(
    int Id,
    string Name,
    double MemoryMb);
