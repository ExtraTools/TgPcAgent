namespace TgPcAgent.Core.Security;

public sealed record ConfirmationAdvanceResult(
    ConfirmationAdvanceOutcome Outcome,
    string? ActionKey,
    int CurrentStep);
