using System.Text.Json;
using System.Text.Json.Serialization;

namespace TgPcAgent.App.Scripts;

public sealed record Script(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("steps")] List<ScriptStep> Steps,
    [property: JsonPropertyName("stopOnError")] bool StopOnError = true
);

public sealed class ScriptStep
{
    [JsonPropertyName("tool")]
    public string? ToolRaw { get; set; }

    [JsonPropertyName("action")]
    public string? ActionRaw { get; set; }

    [JsonPropertyName("params")]
    public JsonElement ParamsRaw { get; set; }

    [JsonPropertyName("args")]
    public JsonElement ArgsRaw { get; set; }

    [JsonPropertyName("ignoreError")]
    public bool IgnoreError { get; set; }

    [JsonIgnore]
    public string Tool => ToolRaw ?? ActionRaw ?? "";

    [JsonIgnore]
    public JsonElement Params =>
        ParamsRaw.ValueKind != JsonValueKind.Undefined ? ParamsRaw : ArgsRaw;
}

public sealed record StepResult(bool Ok, string? Output = null, string? Error = null);

