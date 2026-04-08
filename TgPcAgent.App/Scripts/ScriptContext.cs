using TgPcAgent.App.Services;

namespace TgPcAgent.App.Scripts;

public sealed class ScriptContext
{
    public long ChatId { get; }
    public IResponseSender Sender { get; }
    public ScreenshotService Screenshots { get; }
    public FileLogger Logger { get; }
    public int? StatusMessageId { get; set; }

    public ScriptContext(long chatId, IResponseSender sender, ScreenshotService screenshots, FileLogger logger)
    {
        ChatId = chatId;
        Sender = sender;
        Screenshots = screenshots;
        Logger = logger;
    }
}
