namespace TgPcAgent.Core.Interop;

public static class InstanceControlMessageCodec
{
    public static string Encode(InstanceControlCommand command)
    {
        return command switch
        {
            InstanceControlCommand.ShutdownExisting => "shutdown-existing",
            InstanceControlCommand.RestartExisting => "restart-existing",
            _ => "unknown"
        };
    }

    public static bool TryDecode(string? payload, out InstanceControlCommand command)
    {
        command = payload?.Trim() switch
        {
            "shutdown-existing" => InstanceControlCommand.ShutdownExisting,
            "restart-existing" => InstanceControlCommand.RestartExisting,
            _ => InstanceControlCommand.Unknown
        };

        return command != InstanceControlCommand.Unknown;
    }
}
