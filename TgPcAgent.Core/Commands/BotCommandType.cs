namespace TgPcAgent.Core.Commands;

public enum BotCommandType
{
    Unknown = 0,
    Ping,
    Status,
    Processes,
    Screenshot,
    Apps,
    ScanApps,
    OpenApp,
    Shutdown,
    Restart,
    Sleep,
    Lock,
    Auto,
    Menu,
    Pair,
    Help,
    Script,
    ScriptHelp,
    ScriptStop
}
