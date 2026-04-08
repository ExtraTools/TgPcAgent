namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileExistsParams(string Path);

public sealed class FileExistsTool : ScriptToolBase<FileExistsParams>
{
    public override string Name => "file.exists";
    public override string Description => "Проверить существование файла/папки";

    protected override Task<StepResult> ExecuteTypedAsync(FileExistsParams p, ScriptContext ctx, CancellationToken ct)
    {
        var exists = File.Exists(p.Path) || Directory.Exists(p.Path);
        return Task.FromResult(new StepResult(true, exists ? "Существует" : "Не найден"));
    }
}
