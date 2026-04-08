using System.IO.Compression;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileUnzipParams(string ZipPath, string To);

public sealed class FileUnzipTool : ScriptToolBase<FileUnzipParams>
{
    public override string Name => "file.unzip";
    public override string Description => "Распаковать ZIP в папку";

    protected override Task<StepResult> ExecuteTypedAsync(FileUnzipParams p, ScriptContext ctx, CancellationToken ct)
    {
        if (!File.Exists(p.ZipPath))
            return Task.FromResult(new StepResult(false, Error: "ZIP-файл не найден"));

        Directory.CreateDirectory(p.To);
        ZipFile.ExtractToDirectory(p.ZipPath, p.To, overwriteFiles: true);
        return Task.FromResult(new StepResult(true, $"Распаковано → {p.To}"));
    }
}
