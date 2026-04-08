namespace TgPcAgent.Core.Security;

public enum PairingAttemptOutcome
{
    InvalidCode = 0,
    CodeExpired,
    AlreadyPaired,
    Paired
}
