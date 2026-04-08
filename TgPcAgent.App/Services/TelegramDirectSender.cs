namespace TgPcAgent.App.Services;

/// <summary>
/// Sends responses directly to Telegram using the local bot token.
/// This is the legacy path, used when the agent polls Telegram directly.
/// </summary>
public sealed class TelegramDirectSender : IResponseSender
{
    private readonly ConfigurationService _configurationService;
    private readonly TelegramApiClient _telegramApiClient;
    private readonly FileLogger _logger;

    public TelegramDirectSender(
        ConfigurationService configurationService,
        TelegramApiClient telegramApiClient,
        FileLogger logger)
    {
        _configurationService = configurationService;
        _telegramApiClient = telegramApiClient;
        _logger = logger;
    }

    public async Task SendTextAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken)
    {
        var token = GetToken();
        if (token is null) return;
        await _telegramApiClient.SendMessageAsync(token, chatId, text, replyMarkup, cancellationToken);
    }

    public async Task<int?> SendTextAndGetIdAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken)
    {
        var token = GetToken();
        if (token is null) return null;
        await _telegramApiClient.SendMessageAsync(token, chatId, text, replyMarkup, cancellationToken);
        return null;
    }

    public async Task SendDocumentAsync(long chatId, byte[] content, string fileName, string caption, CancellationToken cancellationToken)
    {
        var token = GetToken();
        if (token is null) return;
        await _telegramApiClient.SendDocumentAsync(token, chatId, content, fileName, caption, cancellationToken);
    }

    public async Task SendPhotoAsync(long chatId, byte[] content, string fileName, string caption, CancellationToken cancellationToken)
    {
        var token = GetToken();
        if (token is null) return;
        await _telegramApiClient.SendPhotoAsync(token, chatId, content, fileName, caption, cancellationToken);
    }

    public async Task EditMessageTextAsync(long chatId, int messageId, string text, object? replyMarkup, CancellationToken cancellationToken)
    {
        var token = GetToken();
        if (token is null) return;

        try
        {
            await _telegramApiClient.EditMessageTextAsync(token, chatId, messageId, text, replyMarkup, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info("Skipped editMessageText because the Telegram message was already up to date.");
        }
    }

    public async Task EditReplyMarkupAsync(long chatId, int messageId, object? replyMarkup, CancellationToken cancellationToken)
    {
        var token = GetToken();
        if (token is null) return;
        await _telegramApiClient.EditMessageReplyMarkupAsync(token, chatId, messageId, replyMarkup, cancellationToken);
    }

    public async Task AnswerCallbackAsync(string callbackQueryId, string? text, CancellationToken cancellationToken)
    {
        var token = GetToken();
        if (token is null) return;
        await _telegramApiClient.AnswerCallbackQueryAsync(token, callbackQueryId, text, cancellationToken);
    }

    private string? GetToken()
    {
        var token = _configurationService.GetBotToken(_configurationService.GetSnapshot());
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.Info("Skipped Telegram API call because bot token is missing.");
        }
        return token;
    }
}
