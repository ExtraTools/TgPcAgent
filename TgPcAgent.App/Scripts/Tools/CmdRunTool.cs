using System.Diagnostics;
using System.Text;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record CmdRunParams(string Command, string? WorkDir = null);

public sealed class CmdRunTool : ScriptToolBase<CmdRunParams>
{
    public override string Name => "cmd.run";
    public override string Description => "Выполнить shell-команду (таймаут 30с, макс 4KB вывод)";

    protected override async Task<StepResult> ExecuteTypedAsync(CmdRunParams p, ScriptContext ctx, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {p.Command}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.GetEncoding(866),
            StandardErrorEncoding = Encoding.GetEncoding(866),
            WorkingDirectory = p.WorkDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        using var process = Process.Start(psi);
        if (process == null)
            return new StepResult(false, Error: "Не удалось запустить процесс");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null && stdout.Length < 4096) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null && stderr.Length < 4096) stderr.AppendLine(e.Data); };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            return new StepResult(false, Error: "Таймаут (30с)");
        }

        var output = stdout.ToString().TrimEnd();
        var error = stderr.ToString().TrimEnd();

        if (output.Length > 4096) output = output[..4096] + "…";
        if (error.Length > 4096) error = error[..4096] + "…";

        if (process.ExitCode != 0)
            return new StepResult(false, Error: $"Exit code {process.ExitCode}: {(string.IsNullOrEmpty(error) ? output : error)}");

        return new StepResult(true, string.IsNullOrEmpty(output) ? $"Exit code 0" : output);
    }
}
