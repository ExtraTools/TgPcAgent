using System.Runtime.InteropServices;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record KeySendParams(string Keys);

public sealed class KeySendTool : ScriptToolBase<KeySendParams>
{
    public override string Name => "key.send";
    public override string Description => "Отправить хоткей (Alt+Tab, Ctrl+C, и т.д.)";

    protected override Task<StepResult> ExecuteTypedAsync(KeySendParams p, ScriptContext ctx, CancellationToken ct)
    {
        SendKeys.SendWait(TranslateCombo(p.Keys));
        return Task.FromResult(new StepResult(true, $"Отправлено: {p.Keys}"));
    }

    private static string TranslateCombo(string combo)
    {
        return combo
            .Replace("Ctrl+", "^", StringComparison.OrdinalIgnoreCase)
            .Replace("Alt+", "%", StringComparison.OrdinalIgnoreCase)
            .Replace("Shift+", "+", StringComparison.OrdinalIgnoreCase)
            .Replace("Enter", "{ENTER}", StringComparison.OrdinalIgnoreCase)
            .Replace("Tab", "{TAB}", StringComparison.OrdinalIgnoreCase)
            .Replace("Esc", "{ESC}", StringComparison.OrdinalIgnoreCase)
            .Replace("Space", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("Delete", "{DEL}", StringComparison.OrdinalIgnoreCase)
            .Replace("Backspace", "{BS}", StringComparison.OrdinalIgnoreCase)
            .Replace("F1", "{F1}", StringComparison.OrdinalIgnoreCase)
            .Replace("F2", "{F2}", StringComparison.OrdinalIgnoreCase)
            .Replace("F3", "{F3}", StringComparison.OrdinalIgnoreCase)
            .Replace("F4", "{F4}", StringComparison.OrdinalIgnoreCase)
            .Replace("F5", "{F5}", StringComparison.OrdinalIgnoreCase)
            .Replace("F11", "{F11}", StringComparison.OrdinalIgnoreCase)
            .Replace("F12", "{F12}", StringComparison.OrdinalIgnoreCase);
    }
}
