namespace TgPcAgent.App.Models;

public sealed class AppConfig
{
    public string? ProtectedBotToken { get; set; }
    
    public string? ProtectedCloudAgentSecret { get; set; }

    /// <summary>Unique agent identifier (UUID), generated on first run.</summary>
    public string? AgentId { get; set; }

    /// <summary>Agent secret for cloud authentication, DPAPI-encrypted.</summary>
    public string? ProtectedAgentSecret { get; set; }

    public long? OwnerChatId { get; set; }
    
    public string? CloudRelayUrl { get; set; }

    public int AutoPingIntervalMinutes { get; set; }

    public int AutoScreenshotIntervalMinutes { get; set; }

    public bool RunAtStartup { get; set; }

    public bool AutoUpdate { get; set; } = true;

    public bool SilentUpdate { get; set; } = true;

    public List<ConfiguredApp> AllowedApps { get; set; } = [];

    public AppConfig Clone()
    {
        return new AppConfig
        {
            ProtectedBotToken = ProtectedBotToken,
            ProtectedCloudAgentSecret = ProtectedCloudAgentSecret,
            AgentId = AgentId,
            ProtectedAgentSecret = ProtectedAgentSecret,
            OwnerChatId = OwnerChatId,
            CloudRelayUrl = CloudRelayUrl,
            AutoPingIntervalMinutes = AutoPingIntervalMinutes,
            AutoScreenshotIntervalMinutes = AutoScreenshotIntervalMinutes,
            RunAtStartup = RunAtStartup,
            AutoUpdate = AutoUpdate,
            SilentUpdate = SilentUpdate,
            AllowedApps = AllowedApps.Select(app => app.Clone()).ToList()
        };
    }
}
