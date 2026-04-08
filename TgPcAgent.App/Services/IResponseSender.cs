namespace TgPcAgent.App.Services;

/// <summary>
/// Abstraction for delivering command results to the user (via Telegram or cloud relay).
/// </summary>
public interface IResponseSender
{
    Task SendTextAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken);

    Task<int?> SendTextAndGetIdAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken);

    Task SendDocumentAsync(long chatId, byte[] content, string fileName, string caption, CancellationToken cancellationToken);

    Task SendPhotoAsync(long chatId, byte[] content, string fileName, string caption, CancellationToken cancellationToken);

    Task EditMessageTextAsync(long chatId, int messageId, string text, object? replyMarkup, CancellationToken cancellationToken);

    Task EditReplyMarkupAsync(long chatId, int messageId, object? replyMarkup, CancellationToken cancellationToken);

    Task AnswerCallbackAsync(string callbackQueryId, string? text, CancellationToken cancellationToken);
}
