namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileSizeParams(string Path);

public sealed class FileSizeTool : ScriptToolBase<FileSizeParams>
{
    public override string Name => "file.size";
    public override string Description => "Размер файла";

    protected override Task<StepResult> ExecuteTypedAsync(FileSizeParams p, ScriptContext ctx, CancellationToken ct)
    {
        if (!File.Exists(p.Path))
            return Task.FromResult(new StepResult(false, Error: "Файл не найден"));

        var fi = new FileInfo(p.Path);
        var sizeMb = fi.Length / (1024.0 * 1024.0);
        return Task.FromResult(new StepResult(true, $"{sizeMb:F2} MB ({fi.Length} bytes)"));
    }
}
