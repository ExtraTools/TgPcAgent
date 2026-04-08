namespace TgPcAgent.App.Services;

public sealed class TrayNotifier
{
    private readonly NotifyIcon _notifyIcon;
    private readonly UiDispatcher _uiDispatcher;

    public TrayNotifier(NotifyIcon notifyIcon, UiDispatcher uiDispatcher)
    {
        _notifyIcon = notifyIcon;
        _uiDispatcher = uiDispatcher;
    }

    public void ShowInfo(string title, string text)
    {
        _ = _uiDispatcher.InvokeAsync(() => _notifyIcon.ShowBalloonTip(4000, title, text, ToolTipIcon.Info));
    }

    public void ShowWarning(string title, string text)
    {
        _ = _uiDispatcher.InvokeAsync(() => _notifyIcon.ShowBalloonTip(5000, title, text, ToolTipIcon.Warning));
    }
}
