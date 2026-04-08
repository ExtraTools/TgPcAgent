using TgPcAgent.App.Services;

namespace TgPcAgent.App.Tests.Services;

public sealed class FirstRunExperiencePolicyTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ShouldOpenSettings_ReturnsExpectedValue(bool configExists, bool expected)
    {
        var policy = new FirstRunExperiencePolicy();

        var result = policy.ShouldOpenSettings(configExists);

        Assert.Equal(expected, result);
    }
}
