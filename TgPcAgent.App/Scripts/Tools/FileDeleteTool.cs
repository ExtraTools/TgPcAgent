namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileDeleteParams(string Path);

public sealed class FileDeleteTool : ScriptToolBase<FileDeleteParams>
{
    public override string Name => "file.delete";
    public override string Description => "Удалить файл или папку";

    protected override Task<StepResult> ExecuteTypedAsync(FileDeleteParams p, ScriptContext ctx, CancellationToken ct)
    {
        if (Directory.Exists(p.Path))
        {
            Directory.Delete(p.Path, recursive: true);
            return Task.FromResult(new StepResult(true, "Папка удалена"));
        }

        if (File.Exists(p.Path))
        {
            File.Delete(p.Path);
            return Task.FromResult(new StepResult(true, "Файл удалён"));
        }

        return Task.FromResult(new StepResult(false, Error: "Файл/папка не найдены"));
    }
}
