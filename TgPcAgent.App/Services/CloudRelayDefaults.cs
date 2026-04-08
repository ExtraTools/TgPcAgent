using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

public static class CloudRelayDefaults
{
    public const string ProductionUrl = "https://tgpcagent-cloud.vercel.app";

    public static void Apply(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.CloudRelayUrl))
        {
            config.CloudRelayUrl = ProductionUrl;
        }
    }
}