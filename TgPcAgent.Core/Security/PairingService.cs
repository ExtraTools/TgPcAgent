using System.Security.Cryptography;

namespace TgPcAgent.Core.Security;

public sealed class PairingService
{
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly object _sync = new();

    private long? _ownerChatId;
    private string? _activeCode;
    private DateTimeOffset _activeCodeExpiresAt;

    public PairingService(Func<DateTimeOffset>? nowProvider = null, long? ownerChatId = null)
    {
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
        _ownerChatId = ownerChatId;
    }

    public long? OwnerChatId
    {
        get
        {
            lock (_sync)
            {
                return _ownerChatId;
            }
        }
    }

    public PairingCodeState IssuePairingCode(TimeSpan lifetime)
    {
        lock (_sync)
        {
            _activeCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _activeCodeExpiresAt = _nowProvider().Add(lifetime);
            return new PairingCodeState(_activeCode, _activeCodeExpiresAt);
        }
    }

    public PairingAttemptResult TryPair(long chatId, string? suppliedCode)
    {
        lock (_sync)
        {
            if (_ownerChatId.HasValue)
            {
                return new PairingAttemptResult(PairingAttemptOutcome.AlreadyPaired, _ownerChatId);
            }

            if (string.IsNullOrWhiteSpace(_activeCode) || string.IsNullOrWhiteSpace(suppliedCode))
            {
                return new PairingAttemptResult(PairingAttemptOutcome.InvalidCode, null);
            }

            if (_activeCodeExpiresAt <= _nowProvider())
            {
                ClearCode();
                return new PairingAttemptResult(PairingAttemptOutcome.CodeExpired, null);
            }

            if (!string.Equals(_activeCode, suppliedCode.Trim(), StringComparison.Ordinal))
            {
                return new PairingAttemptResult(PairingAttemptOutcome.InvalidCode, null);
            }

            _ownerChatId = chatId;
            ClearCode();
            return new PairingAttemptResult(PairingAttemptOutcome.Paired, _ownerChatId);
        }
    }

    public bool IsAuthorized(long chatId)
    {
        lock (_sync)
        {
            return _ownerChatId == chatId;
        }
    }

    public void SetOwner(long chatId)
    {
        lock (_sync)
        {
            _ownerChatId = chatId;
            ClearCode();
        }
    }

    public void ResetOwner()
    {
        lock (_sync)
        {
            _ownerChatId = null;
            ClearCode();
        }
    }

    private void ClearCode()
    {
        _activeCode = null;
        _activeCodeExpiresAt = default;
    }
}
