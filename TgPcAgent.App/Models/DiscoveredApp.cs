namespace TgPcAgent.App.Models;

public sealed record DiscoveredApp(
    string Alias,
    string DisplayName,
    string LaunchTarget,
    string Source);
