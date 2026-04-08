namespace TgPcAgent.Core.Security;

public sealed record PendingConfirmation(
    string Id,
    long ChatId,
    string ActionKey,
    int Step,
    DateTimeOffset ExpiresAt);
