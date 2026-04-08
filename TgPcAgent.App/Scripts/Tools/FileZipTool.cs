using System.IO.Compression;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record FileZipParams(string Path, string ZipPath);

public sealed class FileZipTool : ScriptToolBase<FileZipParams>
{
    public override string Name => "file.zip";
    public override string Description => "Сжать файл/папку в ZIP";

    protected override Task<StepResult> ExecuteTypedAsync(FileZipParams p, ScriptContext ctx, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(p.ZipPath)!);

        if (Directory.Exists(p.Path))
        {
            if (File.Exists(p.ZipPath)) File.Delete(p.ZipPath);
            ZipFile.CreateFromDirectory(p.Path, p.ZipPath);
        }
        else if (File.Exists(p.Path))
        {
            using var zip = ZipFile.Open(p.ZipPath, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(p.Path, Path.GetFileName(p.Path));
        }
        else
        {
            return Task.FromResult(new StepResult(false, Error: "Путь не найден"));
        }

        var size = new FileInfo(p.ZipPath).Length / 1024.0;
        return Task.FromResult(new StepResult(true, $"ZIP создан ({size:F0} KB)"));
    }
}
