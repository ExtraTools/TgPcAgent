using TgPcAgent.Core.Commands;

namespace TgPcAgent.Core.Tests.Commands;

public sealed class BotCommandParserTests
{
    private readonly BotCommandParser _parser = new();

    [Theory]
    [InlineData("/ping", BotCommandType.Ping, null)]
    [InlineData("/status", BotCommandType.Status, null)]
    [InlineData("/processes", BotCommandType.Processes, null)]
    [InlineData("/screenshot", BotCommandType.Screenshot, null)]
    [InlineData("/apps", BotCommandType.Apps, null)]
    [InlineData("/scanapps", BotCommandType.ScanApps, null)]
    [InlineData("/shutdown", BotCommandType.Shutdown, null)]
    [InlineData("/restart", BotCommandType.Restart, null)]
    [InlineData("/sleep", BotCommandType.Sleep, null)]
    [InlineData("/lock", BotCommandType.Lock, null)]
    [InlineData("/menu", BotCommandType.Menu, null)]
    [InlineData("/auto", BotCommandType.Auto, null)]
    public void Parse_KnownCommand_ReturnsExpectedType(string text, BotCommandType expectedType, string? expectedArgument)
    {
        var command = _parser.Parse(text);

        Assert.Equal(expectedType, command.Type);
        Assert.Equal(expectedArgument, command.Argument);
    }

    [Fact]
    public void Parse_OpenCommand_PreservesArgument()
    {
        var command = _parser.Parse("/open Steam");

        Assert.Equal(BotCommandType.OpenApp, command.Type);
        Assert.Equal("Steam", command.Argument);
    }

    [Fact]
    public void Parse_PairCommand_PreservesArgument()
    {
        var command = _parser.Parse("/pair 123456");

        Assert.Equal(BotCommandType.Pair, command.Type);
        Assert.Equal("123456", command.Argument);
    }

    [Fact]
    public void Parse_UnknownText_ReturnsUnknown()
    {
        var command = _parser.Parse("hello there");

        Assert.Equal(BotCommandType.Unknown, command.Type);
        Assert.Equal("hello there", command.Argument);
    }
}
