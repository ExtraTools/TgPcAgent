namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileWriteParams(string Path, string Content);

public sealed class FileWriteTool : ScriptToolBase<FileWriteParams>
{
    public override string Name => "file.write";
    public override string Description => "Записать текст в файл";

    protected override async Task<StepResult> ExecuteTypedAsync(FileWriteParams p, ScriptContext ctx, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(p.Path)!);
        await File.WriteAllTextAsync(p.Path, p.Content, ct);
        return new StepResult(true, $"Записано {p.Content.Length} символов");
    }
}
