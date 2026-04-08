using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TgPcAgent.Core.Security;

public sealed class ConfirmationStore
{
    private readonly ConcurrentDictionary<string, PendingConfirmation> _requests = new();
    private readonly Func<DateTimeOffset> _nowProvider;

    public ConfirmationStore(Func<DateTimeOffset>? nowProvider = null)
    {
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public PendingConfirmation Create(long chatId, string actionKey, TimeSpan lifetime)
    {
        var confirmation = new PendingConfirmation(
            Id: GenerateId(),
            ChatId: chatId,
            ActionKey: actionKey,
            Step: 1,
            ExpiresAt: _nowProvider().Add(lifetime));

        _requests[confirmation.Id] = confirmation;
        return confirmation;
    }

    public ConfirmationAdvanceResult Advance(long chatId, string confirmationId)
    {
        if (!_requests.TryGetValue(confirmationId, out var existing))
        {
            return new ConfirmationAdvanceResult(ConfirmationAdvanceOutcome.NotFound, null, 0);
        }

        if (existing.ChatId != chatId)
        {
            return new ConfirmationAdvanceResult(ConfirmationAdvanceOutcome.WrongChat, existing.ActionKey, existing.Step);
        }

        if (existing.ExpiresAt <= _nowProvider())
        {
            _requests.TryRemove(confirmationId, out _);
            return new ConfirmationAdvanceResult(ConfirmationAdvanceOutcome.Expired, existing.ActionKey, existing.Step);
        }

        if (existing.Step == 1)
        {
            var advanced = existing with { Step = 2 };
            _requests[confirmationId] = advanced;
            return new ConfirmationAdvanceResult(ConfirmationAdvanceOutcome.AwaitingSecondApproval, advanced.ActionKey, advanced.Step);
        }

        _requests.TryRemove(confirmationId, out _);
        return new ConfirmationAdvanceResult(ConfirmationAdvanceOutcome.Confirmed, existing.ActionKey, existing.Step);
    }

    public PendingConfirmation? Get(string confirmationId)
    {
        return _requests.TryGetValue(confirmationId, out var request) ? request : null;
    }

    private static string GenerateId()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}
