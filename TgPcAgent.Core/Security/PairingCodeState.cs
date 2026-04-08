namespace TgPcAgent.Core.Security;

public sealed record PairingCodeState(string Code, DateTimeOffset ExpiresAt);
