using System.Text.Json;

namespace TgPcAgent.App.Scripts;

public interface IScriptTool
{
    string Name { get; }
    string Description { get; }
    Task<StepResult> ExecuteAsync(JsonElement parameters, ScriptContext ctx, CancellationToken ct);
}
