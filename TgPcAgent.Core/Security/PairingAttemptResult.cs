namespace TgPcAgent.Core.Security;

public sealed record PairingAttemptResult(PairingAttemptOutcome Outcome, long? OwnerChatId);
