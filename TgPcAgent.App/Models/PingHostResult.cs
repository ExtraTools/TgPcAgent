namespace TgPcAgent.App.Models;

public sealed record PingHostResult(
    string Host,
    long? RoundtripTimeMs,
    bool Success,
    string? Error);
