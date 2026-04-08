namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileListParams(string Path);

public sealed class FileListTool : ScriptToolBase<FileListParams>
{
    public override string Name => "file.list";
    public override string Description => "Список файлов в папке";

    protected override Task<StepResult> ExecuteTypedAsync(FileListParams p, ScriptContext ctx, CancellationToken ct)
    {
        if (!Directory.Exists(p.Path))
            return Task.FromResult(new StepResult(false, Error: "Папка не найдена"));

        var entries = Directory.GetFileSystemEntries(p.Path)
            .Select(e => System.IO.Path.GetFileName(e))
            .Take(50)
            .ToArray();

        return Task.FromResult(new StepResult(true, $"{entries.Length} элементов: {string.Join(", ", entries)}"));
    }
}
