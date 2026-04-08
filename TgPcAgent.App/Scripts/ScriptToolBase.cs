using System.Text.Json;

namespace TgPcAgent.App.Scripts;

public abstract class ScriptToolBase<TParams> : IScriptTool where TParams : class
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public abstract string Name { get; }
    public abstract string Description { get; }

    public async Task<StepResult> ExecuteAsync(JsonElement parameters, ScriptContext ctx, CancellationToken ct)
    {
        try
        {
            var p = parameters.Deserialize<TParams>(JsonOpts);
            if (p == null)
                return new StepResult(false, Error: "Некорректные параметры");
            return await ExecuteTypedAsync(p, ctx, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new StepResult(false, Error: ex.Message);
        }
    }

    protected abstract Task<StepResult> ExecuteTypedAsync(TParams p, ScriptContext ctx, CancellationToken ct);
}
