namespace TgPcAgent.Core.Security;

public enum ConfirmationAdvanceOutcome
{
    NotFound = 0,
    WrongChat,
    Expired,
    AwaitingSecondApproval,
    Confirmed
}
