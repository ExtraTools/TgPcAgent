namespace TgPcAgent.App.Models;

/// <summary>
/// Result of a command execution, sent back to the cloud for delivery to Telegram.
/// </summary>
public sealed class CloudCommandResult
{
    public string AgentId { get; set; } = "";
    public string Secret { get; set; } = "";
    public string? CommandId { get; set; }
    public long ChatId { get; set; }
    public string? Text { get; set; }
    public string? PhotoBase64 { get; set; }
    public string? PhotoFileName { get; set; }
    public string? DocumentBase64 { get; set; }
    public string? DocumentFileName { get; set; }
    public object? ReplyMarkup { get; set; }
    public string? CallbackQueryId { get; set; }
    public string? CallbackText { get; set; }
    public int? EditMessageId { get; set; }
    public string? EditText { get; set; }
    public object? EditReplyMarkup { get; set; }
    public int? PendingMessageId { get; set; }
}
