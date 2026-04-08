namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileRenameParams(string Path, string NewName);

public sealed class FileRenameTool : ScriptToolBase<FileRenameParams>
{
    public override string Name => "file.rename";
    public override string Description => "Переименовать файл";

    protected override Task<StepResult> ExecuteTypedAsync(FileRenameParams p, ScriptContext ctx, CancellationToken ct)
    {
        if (!File.Exists(p.Path))
            return Task.FromResult(new StepResult(false, Error: "Файл не найден"));

        var dir = System.IO.Path.GetDirectoryName(p.Path)!;
        var newPath = System.IO.Path.Combine(dir, p.NewName);
        File.Move(p.Path, newPath);
        return Task.FromResult(new StepResult(true, $"Переименован → {p.NewName}"));
    }
}
