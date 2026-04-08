namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileReadParams(string Path);

public sealed class FileReadTool : ScriptToolBase<FileReadParams>
{
    public override string Name => "file.read";
    public override string Description => "Прочитать текстовый файл (макс 4KB)";

    protected override async Task<StepResult> ExecuteTypedAsync(FileReadParams p, ScriptContext ctx, CancellationToken ct)
    {
        if (!File.Exists(p.Path))
            return new StepResult(false, Error: "Файл не найден");

        var content = await File.ReadAllTextAsync(p.Path, ct);
        if (content.Length > 4096)
            content = content[..4096] + "… (обрезано)";

        return new StepResult(true, content);
    }
}
