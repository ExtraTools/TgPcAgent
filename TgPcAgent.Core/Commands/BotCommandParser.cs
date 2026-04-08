namespace TgPcAgent.Core.Commands;

public sealed class BotCommandParser
{
    public BotCommand Parse(string? text)
    {
        var normalizedText = text?.Trim() ?? string.Empty;

        if (!normalizedText.StartsWith('/'))
        {
            return new BotCommand(BotCommandType.Unknown, normalizedText, string.IsNullOrWhiteSpace(normalizedText) ? null : normalizedText);
        }

        var commandParts = normalizedText.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var rawCommand = commandParts[0];
        var commandName = rawCommand.Split('@', 2)[0].ToLowerInvariant();
        var argument = commandParts.Length > 1 ? commandParts[1] : null;

        var commandType = commandName switch
        {
            "/ping" => BotCommandType.Ping,
            "/status" => BotCommandType.Status,
            "/processes" => BotCommandType.Processes,
            "/screenshot" => BotCommandType.Screenshot,
            "/apps" => BotCommandType.Apps,
            "/scanapps" => BotCommandType.ScanApps,
            "/open" => BotCommandType.OpenApp,
            "/shutdown" => BotCommandType.Shutdown,
            "/restart" => BotCommandType.Restart,
            "/sleep" => BotCommandType.Sleep,
            "/lock" => BotCommandType.Lock,
            "/auto" => BotCommandType.Auto,
            "/menu" => BotCommandType.Menu,
            "/pair" => BotCommandType.Pair,
            "/help" => BotCommandType.Help,
            "/script" => BotCommandType.Script,
            "/scripthelp" => BotCommandType.ScriptHelp,
            "/scriptstop" => BotCommandType.ScriptStop,
            _ => BotCommandType.Unknown
        };

        return new BotCommand(commandType, normalizedText, argument);
    }
}
