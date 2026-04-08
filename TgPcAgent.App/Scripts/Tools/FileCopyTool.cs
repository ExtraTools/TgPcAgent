namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileCopyParams(string From, string To);

public sealed class FileCopyTool : ScriptToolBase<FileCopyParams>
{
    public override string Name => "file.copy";
    public override string Description => "Копировать файл";

    protected override Task<StepResult> ExecuteTypedAsync(FileCopyParams p, ScriptContext ctx, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(p.To)!);
        File.Copy(p.From, p.To, overwrite: true);
        return Task.FromResult(new StepResult(true, $"Скопировано → {Path.GetFileName(p.To)}"));
    }
}
