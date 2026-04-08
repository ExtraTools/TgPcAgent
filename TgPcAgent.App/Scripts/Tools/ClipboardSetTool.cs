namespace TgPcAgent.App.Scripts.Tools;

public sealed record ClipboardSetParams(string Text);

public sealed class ClipboardSetTool : ScriptToolBase<ClipboardSetParams>
{
    public override string Name => "clipboard.set";
    public override string Description => "Установить содержимое буфера обмена";

    protected override Task<StepResult> ExecuteTypedAsync(ClipboardSetParams p, ScriptContext ctx, CancellationToken ct)
    {
        var thread = new Thread(() => Clipboard.SetText(p.Text));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(3000);

        return Task.FromResult(new StepResult(true, $"Установлено ({p.Text.Length} символов)"));
    }
}
