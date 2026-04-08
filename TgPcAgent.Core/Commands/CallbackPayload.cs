namespace TgPcAgent.Core.Commands;

public sealed record CallbackPayload(CallbackActionType Type, string? Value, string RawData);
