namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileMoveParams(string From, string To);

public sealed class FileMoveTool : ScriptToolBase<FileMoveParams>
{
    public override string Name => "file.move";
    public override string Description => "Переместить файл";

    protected override Task<StepResult> ExecuteTypedAsync(FileMoveParams p, ScriptContext ctx, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(p.To)!);
        File.Move(p.From, p.To, overwrite: true);
        return Task.FromResult(new StepResult(true, $"Перемещено → {Path.GetFileName(p.To)}"));
    }
}
