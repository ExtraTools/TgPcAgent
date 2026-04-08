using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

/// <summary>
/// Sends command results to the cloud relay for delivery to Telegram.
/// Used in shared-bot mode where the agent doesn't have direct Telegram access.
/// </summary>
public sealed class CloudRelayResultSender : IResponseSender
{
    private readonly CloudApiClient _cloudApiClient;
    private readonly ConfigurationService _configurationService;
    private readonly FileLogger _logger;

    public CloudRelayResultSender(
        CloudApiClient cloudApiClient,
        ConfigurationService configurationService,
        FileLogger logger)
    {
        _cloudApiClient = cloudApiClient;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task SendTextAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken)
    {
        var result = CreateBaseResult(chatId);
        result.Text = text;
        result.ReplyMarkup = replyMarkup;
        await SendAsync(result, cancellationToken);
    }

    public async Task<int?> SendTextAndGetIdAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken)
    {
        var result = CreateBaseResult(chatId);
        result.Text = text;
        result.ReplyMarkup = replyMarkup;
        return await SendAndGetMessageIdAsync(result, cancellationToken);
    }

    public async Task SendDocumentAsync(long chatId, byte[] content, string fileName, string caption, CancellationToken cancellationToken)
    {
        var result = CreateBaseResult(chatId);
        result.Text = caption;
        result.DocumentBase64 = Convert.ToBase64String(content);
        result.DocumentFileName = fileName;
        await SendAsync(result, cancellationToken);
    }

    public async Task SendPhotoAsync(long chatId, byte[] content, string fileName, string caption, CancellationToken cancellationToken)
    {
        var result = CreateBaseResult(chatId);
        result.Text = caption;
        result.PhotoBase64 = Convert.ToBase64String(content);
        result.PhotoFileName = fileName;
        await SendAsync(result, cancellationToken);
    }

    public async Task EditMessageTextAsync(long chatId, int messageId, string text, object? replyMarkup, CancellationToken cancellationToken)
    {
        var result = CreateBaseResult(chatId);
        result.EditMessageId = messageId;
        result.EditText = text;
        result.EditReplyMarkup = replyMarkup;
        await SendAsync(result, cancellationToken);
    }

    public async Task EditReplyMarkupAsync(long chatId, int messageId, object? replyMarkup, CancellationToken cancellationToken)
    {
        // EditReplyMarkup is done via editMessageText with empty text update through cloud
        // Cloud's result endpoint handles editMessageText which also sets reply_markup
        var result = CreateBaseResult(chatId);
        result.EditMessageId = messageId;
        result.EditReplyMarkup = replyMarkup;
        await SendAsync(result, cancellationToken);
    }

    public async Task AnswerCallbackAsync(string callbackQueryId, string? text, CancellationToken cancellationToken)
    {
        var result = CreateBaseResult(0);
        result.CallbackQueryId = callbackQueryId;
        result.CallbackText = text ?? "";
        await SendAsync(result, cancellationToken);
    }

    /// <summary>
    /// Associates a specific command ID with the next result to be sent.
    /// Called before the command execution starts.
    /// </summary>
    public void SetCurrentCommandId(string commandId)
    {
        _pendingCommandId = commandId;
    }

    public void SetPendingMessageId(int? messageId)
    {
        _pendingMessageId = messageId;
    }

    public void ClearCurrentCommandId()
    {
        _pendingCommandId = null;
        _pendingMessageId = null;
    }

    private string? _pendingCommandId;
    private int? _pendingMessageId;

    private CloudCommandResult CreateBaseResult(long chatId)
    {
        var config = _configurationService.GetSnapshot();
        return new CloudCommandResult
        {
            AgentId = config.AgentId ?? "",
            Secret = _configurationService.GetAgentSecret(config) ?? "",
            CommandId = _pendingCommandId,
            ChatId = chatId,
            PendingMessageId = _pendingMessageId
        };
    }

    private async Task SendAsync(CloudCommandResult result, CancellationToken cancellationToken)
    {
        var config = _configurationService.GetSnapshot();
        var baseUrl = config.CloudRelayUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.Info("Skipped cloud result because CloudRelayUrl is empty.");
            return;
        }

        if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute, out var baseUri))
        {
            _logger.Info("Skipped cloud result because CloudRelayUrl is invalid.");
            return;
        }

        await _cloudApiClient.SendResultAsync(baseUri, result, cancellationToken);
    }

    private async Task<int?> SendAndGetMessageIdAsync(CloudCommandResult result, CancellationToken cancellationToken)
    {
        var config = _configurationService.GetSnapshot();
        var baseUrl = config.CloudRelayUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute, out var baseUri)) return null;

        var response = await _cloudApiClient.SendResultAsync(baseUri, result, cancellationToken);
        return response?.MessageId;
    }
}
