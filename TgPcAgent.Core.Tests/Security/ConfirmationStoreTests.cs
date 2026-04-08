using TgPcAgent.Core.Security;

namespace TgPcAgent.Core.Tests.Security;

public sealed class ConfirmationStoreTests
{
    private static readonly DateTimeOffset FrozenNow = new(2026, 4, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Advance_FirstStep_MovesConfirmationToSecondStep()
    {
        var store = new ConfirmationStore(() => FrozenNow);
        var request = store.Create(1234, "shutdown", TimeSpan.FromMinutes(1));

        var result = store.Advance(1234, request.Id);

        Assert.Equal(ConfirmationAdvanceOutcome.AwaitingSecondApproval, result.Outcome);
        Assert.Equal(2, result.CurrentStep);
        Assert.Equal("shutdown", result.ActionKey);
    }

    [Fact]
    public void Advance_SecondStep_CompletesConfirmation()
    {
        var store = new ConfirmationStore(() => FrozenNow);
        var request = store.Create(1234, "restart", TimeSpan.FromMinutes(1));

        store.Advance(1234, request.Id);
        var result = store.Advance(1234, request.Id);

        Assert.Equal(ConfirmationAdvanceOutcome.Confirmed, result.Outcome);
        Assert.Null(store.Get(request.Id));
    }

    [Fact]
    public void Advance_WrongChat_IsRejected()
    {
        var store = new ConfirmationStore(() => FrozenNow);
        var request = store.Create(1234, "shutdown", TimeSpan.FromMinutes(1));

        var result = store.Advance(9999, request.Id);

        Assert.Equal(ConfirmationAdvanceOutcome.WrongChat, result.Outcome);
        Assert.Equal(1, result.CurrentStep);
    }

    [Fact]
    public void Advance_ExpiredConfirmation_IsRejected()
    {
        var currentTime = FrozenNow;
        var store = new ConfirmationStore(() => currentTime);
        var request = store.Create(1234, "shutdown", TimeSpan.FromSeconds(10));

        currentTime = FrozenNow.AddSeconds(11);
        var result = store.Advance(1234, request.Id);

        Assert.Equal(ConfirmationAdvanceOutcome.Expired, result.Outcome);
        Assert.Null(store.Get(request.Id));
    }
}
