using System.Runtime.InteropServices;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record ClipboardGetParams;

public sealed class ClipboardGetTool : ScriptToolBase<ClipboardGetParams>
{
    public override string Name => "clipboard.get";
    public override string Description => "Получить содержимое буфера обмена";

    protected override Task<StepResult> ExecuteTypedAsync(ClipboardGetParams p, ScriptContext ctx, CancellationToken ct)
    {
        string? text = null;
        var thread = new Thread(() =>
        {
            if (Clipboard.ContainsText())
                text = Clipboard.GetText();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(3000);

        if (text == null)
            return Task.FromResult(new StepResult(true, "(пусто)"));

        if (text.Length > 4096)
            text = text[..4096] + "… (обрезано)";

        return Task.FromResult(new StepResult(true, text));
    }
}
