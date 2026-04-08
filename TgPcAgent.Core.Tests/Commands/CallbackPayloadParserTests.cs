using TgPcAgent.Core.Commands;

namespace TgPcAgent.Core.Tests.Commands;

public sealed class CallbackPayloadParserTests
{
    private readonly CallbackPayloadParser _parser = new();

    [Fact]
    public void Parse_ConfirmPayload_ReturnsConfirmationAction()
    {
        var payload = _parser.Parse("confirm:deadbeef");

        Assert.Equal(CallbackActionType.Confirm, payload.Type);
        Assert.Equal("deadbeef", payload.Value);
    }

    [Fact]
    public void Parse_ScanAppsPagePayload_ReturnsPageAction()
    {
        var payload = _parser.Parse("scanapps:page:3");

        Assert.Equal(CallbackActionType.ScanAppsPage, payload.Type);
        Assert.Equal("3", payload.Value);
    }

    [Fact]
    public void Parse_ScanAppsOpenPayload_ReturnsOpenAction()
    {
        var payload = _parser.Parse("scanapps:open:steam");

        Assert.Equal(CallbackActionType.OpenApp, payload.Type);
        Assert.Equal("steam", payload.Value);
    }

    [Theory]
    [InlineData("auto:open", "open")]
    [InlineData("auto:set:ping:5", "set:ping:5")]
    [InlineData("auto:set:screenshot:1", "set:screenshot:1")]
    public void Parse_AutomationPayload_ReturnsAutomationAction(string input, string expectedValue)
    {
        var payload = _parser.Parse(input);

        Assert.Equal(CallbackActionType.Automation, payload.Type);
        Assert.Equal(expectedValue, payload.Value);
    }

    [Fact]
    public void Parse_UnknownPayload_ReturnsUnknown()
    {
        var payload = _parser.Parse("weird:data");

        Assert.Equal(CallbackActionType.Unknown, payload.Type);
        Assert.Equal("weird:data", payload.Value);
    }
}
