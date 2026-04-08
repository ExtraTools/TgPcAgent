using TgPcAgent.Core.Security;

namespace TgPcAgent.Core.Tests.Security;

public sealed class PairingServiceTests
{
    private static readonly DateTimeOffset FrozenNow = new(2026, 4, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IssuePairingCode_ReturnsSixDigitCode()
    {
        var service = new PairingService(() => FrozenNow);

        var state = service.IssuePairingCode(TimeSpan.FromMinutes(5));

        Assert.Matches("^[0-9]{6}$", state.Code);
        Assert.True(state.ExpiresAt > FrozenNow);
    }

    [Fact]
    public void TryPair_CorrectCode_BindsOwner()
    {
        var service = new PairingService(() => FrozenNow);
        var state = service.IssuePairingCode(TimeSpan.FromMinutes(5));

        var result = service.TryPair(4242, state.Code);

        Assert.Equal(PairingAttemptOutcome.Paired, result.Outcome);
        Assert.True(service.IsAuthorized(4242));
    }

    [Fact]
    public void TryPair_UsedCode_CannotBeReused()
    {
        var service = new PairingService(() => FrozenNow);
        var state = service.IssuePairingCode(TimeSpan.FromMinutes(5));

        service.TryPair(4242, state.Code);
        var secondAttempt = service.TryPair(8888, state.Code);

        Assert.Equal(PairingAttemptOutcome.AlreadyPaired, secondAttempt.Outcome);
        Assert.False(service.IsAuthorized(8888));
    }

    [Fact]
    public void TryPair_ExpiredCode_IsRejected()
    {
        var currentTime = FrozenNow;
        var service = new PairingService(() => currentTime);
        var state = service.IssuePairingCode(TimeSpan.FromSeconds(10));

        currentTime = FrozenNow.AddSeconds(15);
        var result = service.TryPair(4242, state.Code);

        Assert.Equal(PairingAttemptOutcome.CodeExpired, result.Outcome);
        Assert.False(service.IsAuthorized(4242));
    }
}
