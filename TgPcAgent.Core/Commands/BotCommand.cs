namespace TgPcAgent.Core.Commands;

public sealed record BotCommand(BotCommandType Type, string RawText, string? Argument);
