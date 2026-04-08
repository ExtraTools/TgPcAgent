namespace TgPcAgent.App.Scripts.Tools;

public sealed record TypeTextParams(string Text);

public sealed class TypeTextTool : ScriptToolBase<TypeTextParams>
{
    public override string Name => "type.text";
    public override string Description => "Напечатать текст в активном окне";

    protected override Task<StepResult> ExecuteTypedAsync(TypeTextParams p, ScriptContext ctx, CancellationToken ct)
    {
        SendKeys.SendWait(EscapeForSendKeys(p.Text));
        return Task.FromResult(new StepResult(true, $"Напечатано ({p.Text.Length} символов)"));
    }

    private static string EscapeForSendKeys(string text)
    {
        return text
            .Replace("{", "{{}")
            .Replace("}", "{}}")
            .Replace("(", "{(}")
            .Replace(")", "{)}")
            .Replace("+", "{+}")
            .Replace("^", "{^}")
            .Replace("%", "{%}")
            .Replace("~", "{~}");
    }
}
