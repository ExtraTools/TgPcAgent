namespace TgPcAgent.App.Models;

/// <summary>
/// A command received from the cloud relay, originally sent by a user in Telegram.
/// </summary>
public sealed class CloudCommand
{
    public string Id { get; set; } = "";
    public long ChatId { get; set; }
    public string Text { get; set; } = "";
    public string Type { get; set; } = "";
    public string? CallbackQueryId { get; set; }
    public int? MessageId { get; set; }
    public int? PendingMessageId { get; set; }
    public string CreatedAt { get; set; } = "";
}
