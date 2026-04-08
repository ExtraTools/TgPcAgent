using TgPcAgent.Core.Automation;

namespace TgPcAgent.Core.Tests.Automation;

public sealed class AutomationIntervalCatalogTests
{
    [Fact]
    public void SupportedMinutes_ReturnsExpectedPresetList()
    {
        Assert.Equal([0, 1, 5, 15, 30, 60], AutomationIntervalCatalog.SupportedMinutes);
    }

    [Theory]
    [InlineData(0, "Выкл")]
    [InlineData(1, "1 мин")]
    [InlineData(5, "5 мин")]
    [InlineData(60, "1 ч")]
    public void FormatLabel_ReturnsHumanReadableText(int minutes, string expected)
    {
        Assert.Equal(expected, AutomationIntervalCatalog.FormatLabel(minutes));
    }

    [Fact]
    public void IsSupported_ReturnsFalseForUnsupportedValues()
    {
        Assert.False(AutomationIntervalCatalog.IsSupported(2));
    }
}
