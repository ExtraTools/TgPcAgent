using TgPcAgent.App.Models;
using TgPcAgent.App.Services;

namespace TgPcAgent.App.Tests.Services;

public sealed class CloudRelayDefaultsTests
{
    [Fact]
    public void Apply_UsesProductionUrl_WhenConfigValueIsMissing()
    {
        var config = new AppConfig
        {
            CloudRelayUrl = null
        };

        CloudRelayDefaults.Apply(config);

        Assert.Equal(CloudRelayDefaults.ProductionUrl, config.CloudRelayUrl);
    }

    [Fact]
    public void Apply_UsesProductionUrl_WhenConfigValueIsWhitespace()
    {
        var config = new AppConfig
        {
            CloudRelayUrl = "   "
        };

        CloudRelayDefaults.Apply(config);

        Assert.Equal(CloudRelayDefaults.ProductionUrl, config.CloudRelayUrl);
    }

    [Fact]
    public void Apply_PreservesExistingValue_WhenAlreadySet()
    {
        var config = new AppConfig
        {
            CloudRelayUrl = "https://custom.example.com"
        };

        CloudRelayDefaults.Apply(config);

        Assert.Equal("https://custom.example.com", config.CloudRelayUrl);
    }
}
