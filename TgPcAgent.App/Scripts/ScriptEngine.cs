using System.Diagnostics;
using System.Net;
using System.Text.Json;
using TgPcAgent.App.Services;

namespace TgPcAgent.App.Scripts;

public sealed class ScriptEngine
{
    private const int MaxSteps = 20;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, IScriptTool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileLogger _logger;
    private CancellationTokenSource? _activeCts;

    public bool IsRunning => _activeCts is { IsCancellationRequested: false };

    public ScriptEngine(FileLogger logger)
    {
        _logger = logger;
    }

    public void RegisterTool(IScriptTool tool)
    {
        _tools[tool.Name] = tool;
    }

    public void Stop()
    {
        _activeCts?.Cancel();
    }

    public IReadOnlyDictionary<string, IScriptTool> Tools => _tools;

    public async Task ExecuteAsync(string rawText, ScriptContext ctx, CancellationToken externalCt)
    {
        var json = ExtractJson(rawText);
        Script script;
        try
        {
            script = JsonSerializer.Deserialize<Script>(json, JsonOpts)
                     ?? throw new JsonException("Пустой результат десериализации");

            if (script.Steps == null || script.Steps.Count == 0)
                throw new JsonException("Скрипт не содержит шагов");
        }
        catch (JsonException ex)
        {
            await ctx.Sender.SendTextAsync(ctx.ChatId,
                $"<b>❌ Ошибка парсинга скрипта</b>\n<code>{WebUtility.HtmlEncode(ex.Message)}</code>", null, externalCt);
            return;
        }

        var validationError = Validate(script);
        if (validationError != null)
        {
            await ctx.Sender.SendTextAsync(ctx.ChatId,
                $"<b>❌ Ошибка валидации</b>\n{WebUtility.HtmlEncode(validationError)}", null, externalCt);
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _activeCts = cts;

        var results = new List<StepResult>();
        var stepTimes = new List<TimeSpan>();
        var totalSw = Stopwatch.StartNew();
        var totalSteps = script.Steps.Count;

        var statusText = BuildProgressMessage(script.Name, script.Steps, results, stepTimes, -1, totalSteps);
        var msgId = await ctx.Sender.SendTextAndGetIdAsync(ctx.ChatId, statusText, null, cts.Token);
        ctx.StatusMessageId = msgId;

        bool cancelled = false;
        bool stoppedOnError = false;

        for (int i = 0; i < totalSteps; i++)
        {
            if (cts.Token.IsCancellationRequested)
            {
                cancelled = true;
                for (int j = i; j < totalSteps; j++)
                {
                    results.Add(new StepResult(false, Error: "Прервано"));
                    stepTimes.Add(TimeSpan.Zero);
                }
                break;
            }

            var step = script.Steps[i];

            statusText = BuildProgressMessage(script.Name, script.Steps, results, stepTimes, i, totalSteps);
            await TryEditMessage(ctx, msgId, statusText, cts.Token);

            var stepSw = Stopwatch.StartNew();
            StepResult result;
            try
            {
                if (!_tools.TryGetValue(step.Tool, out var tool))
                {
                    result = new StepResult(false, Error: $"Неизвестный инструмент: {step.Tool}");
                    _logger.Info($"Script [{i + 1}/{totalSteps}] {step.Tool} -> UNKNOWN TOOL");
                }
                else
                {
                    var paramsStr = step.Params.ValueKind != System.Text.Json.JsonValueKind.Undefined
                        ? step.Params.ToString() : "{}";
                    _logger.Info($"Script [{i + 1}/{totalSteps}] {step.Tool} params={paramsStr}");
                    result = await tool.ExecuteAsync(step.Params, ctx, cts.Token);
                    if (result.Ok)
                        _logger.Info($"Script [{i + 1}/{totalSteps}] {step.Tool} -> OK: {result.Output ?? "(no output)"}");
                    else
                        _logger.Info($"Script [{i + 1}/{totalSteps}] {step.Tool} -> FAIL: {result.Error ?? "(no error)"}");
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                _logger.Info($"Script [{i + 1}/{totalSteps}] {step.Tool} -> CANCELLED");
                stepSw.Stop();
                stepTimes.Add(stepSw.Elapsed);
                results.Add(new StepResult(false, Error: "Прервано"));
                for (int j = i + 1; j < totalSteps; j++)
                {
                    results.Add(new StepResult(false, Error: "Прервано"));
                    stepTimes.Add(TimeSpan.Zero);
                }
                break;
            }
            catch (Exception ex)
            {
                _logger.Error($"Script [{i + 1}/{totalSteps}] {step.Tool} EXCEPTION", ex);
                result = new StepResult(false, Error: ex.Message);
            }

            stepSw.Stop();
            stepTimes.Add(stepSw.Elapsed);
            results.Add(result);

            statusText = BuildProgressMessage(script.Name, script.Steps, results, stepTimes, -1, totalSteps);
            await TryEditMessage(ctx, msgId, statusText, cts.Token);

            if (!result.Ok && !step.IgnoreError && script.StopOnError)
            {
                stoppedOnError = true;
                for (int j = i + 1; j < totalSteps; j++)
                {
                    results.Add(new StepResult(false, Error: "Пропущен (stopOnError)"));
                    stepTimes.Add(TimeSpan.Zero);
                }
                break;
            }
        }

        totalSw.Stop();
        _activeCts = null;

        var finalText = BuildFinalMessage(script.Name, script.Steps, results, stepTimes, totalSteps, totalSw.Elapsed, cancelled, stoppedOnError);
        await TryEditMessage(ctx, msgId, finalText, externalCt);
    }

    private static string ExtractJson(string raw)
    {
        var text = raw.Trim();
        var fenceIdx = text.IndexOf("```", StringComparison.Ordinal);
        if (fenceIdx >= 0)
        {
            var afterFence = text.IndexOf('\n', fenceIdx);
            if (afterFence > 0) text = text[(afterFence + 1)..];
            var closeFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (closeFence > 0) text = text[..closeFence];
            text = text.Trim();
        }

        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return text[firstBrace..(lastBrace + 1)];
        return text;
    }

    private string? Validate(Script script)
    {
        if (string.IsNullOrWhiteSpace(script.Name))
            return "Скрипт должен иметь название (name)";
        if (script.Steps.Count > MaxSteps)
            return $"Максимум {MaxSteps} шагов, получено {script.Steps.Count}";
        return null;
    }

    private string BuildProgressMessage(string name, List<ScriptStep> steps, List<StepResult> results, List<TimeSpan> stepTimes, int currentIdx, int total)
    {
        var lines = new List<string>
        {
            $"<b>📋 Скрипт: {WebUtility.HtmlEncode(name)}</b>",
            $"Статус: Выполнение... ⏳\n"
        };

        for (int i = 0; i < total; i++)
        {
            var toolName = steps[i].Tool;
            var paramHint = GetParamHint(steps[i]);
            if (i < results.Count)
            {
                var r = results[i];
                var elapsed = i < stepTimes.Count ? stepTimes[i] : TimeSpan.Zero;
                var timeStr = $" <i>{elapsed.TotalSeconds:F1}s</i>";
                if (r.Ok)
                {
                    var detail = r.Output ?? "OK";
                    if (detail.Length > 60) detail = detail[..60] + "...";
                    lines.Add($"🟢 [{i + 1}/{total}] <code>{WebUtility.HtmlEncode(toolName)}</code>{paramHint}{timeStr}\n     {WebUtility.HtmlEncode(detail)}");
                }
                else
                {
                    var icon = steps[i].IgnoreError ? "🟡" : "🔴";
                    var err = r.Error ?? "ошибка";
                    if (err.Length > 120) err = err[..120] + "...";
                    lines.Add($"{icon} [{i + 1}/{total}] <code>{WebUtility.HtmlEncode(toolName)}</code>{paramHint}{timeStr}\n     <code>{WebUtility.HtmlEncode(err)}</code>");
                }
            }
            else if (i == currentIdx)
            {
                lines.Add($"⏳ [{i + 1}/{total}] <code>{WebUtility.HtmlEncode(toolName)}</code>{paramHint}\n     выполняется...");
            }
            else
            {
                lines.Add($"⬜ [{i + 1}/{total}] <code>{WebUtility.HtmlEncode(toolName)}</code>{paramHint}");
            }
        }

        return string.Join('\n', lines);
    }

    private static string BuildFinalMessage(string name, List<ScriptStep> steps, List<StepResult> results, List<TimeSpan> stepTimes, int total, TimeSpan elapsed, bool cancelled, bool stoppedOnError)
    {
        var lines = new List<string>
        {
            $"<b>📋 Скрипт: {WebUtility.HtmlEncode(name)}</b>",
        };

        var successCount = results.Count(r => r.Ok);

        if (cancelled)
            lines.Add("Статус: ⛔️ Прерван\n");
        else if (stoppedOnError)
            lines.Add("Статус: ❌ Остановлен из-за ошибки\n");
        else
            lines.Add("Статус: ✅ Завершён\n");

        for (int i = 0; i < results.Count && i < total; i++)
        {
            var r = results[i];
            var toolName = steps[i].Tool;
            var paramHint = GetParamHint(steps[i]);
            var stepTime = i < stepTimes.Count ? stepTimes[i] : TimeSpan.Zero;
            var timeStr = stepTime > TimeSpan.Zero ? $" <i>{stepTime.TotalSeconds:F1}s</i>" : "";

            if (r.Ok)
            {
                var detail = r.Output ?? "OK";
                if (detail.Length > 80) detail = detail[..80] + "...";
                lines.Add($"🟢 [{i + 1}/{total}] <code>{WebUtility.HtmlEncode(toolName)}</code>{paramHint}{timeStr}\n     {WebUtility.HtmlEncode(detail)}");
            }
            else if (r.Error == "Пропущен (stopOnError)")
            {
                lines.Add($"⏭ [{i + 1}/{total}] <code>{WebUtility.HtmlEncode(toolName)}</code>{paramHint}\n     пропущен");
            }
            else if (r.Error == "Прервано")
            {
                lines.Add($"⛔️ [{i + 1}/{total}] <code>{WebUtility.HtmlEncode(toolName)}</code>{paramHint}\n     прервано");
            }
            else
            {
                var icon = steps[i].IgnoreError ? "🟡" : "🔴";
                var err = r.Error ?? "ошибка";
                if (err.Length > 150) err = err[..150] + "...";
                lines.Add($"{icon} [{i + 1}/{total}] <code>{WebUtility.HtmlEncode(toolName)}</code>{paramHint}{timeStr}\n     <code>{WebUtility.HtmlEncode(err)}</code>");
            }
        }

        lines.Add($"\n⏱ Время: <code>{elapsed.TotalSeconds:F1}s</code> | Успешно: <code>{successCount}/{total}</code>");

        return string.Join('\n', lines);
    }

    private async Task TryEditMessage(ScriptContext ctx, int? msgId, string text, CancellationToken ct)
    {
        if (!msgId.HasValue)
        {
            _logger.Info("Script TryEditMessage: msgId is null, skipping");
            return;
        }
        try
        {
            await ctx.Sender.EditMessageTextAsync(ctx.ChatId, msgId.Value, text, null, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Script TryEditMessage failed (msgId={msgId.Value})", ex);
        }
    }

    private static string GetParamHint(ScriptStep step)
    {
        if (step.Params.ValueKind == System.Text.Json.JsonValueKind.Undefined ||
            step.Params.ValueKind == System.Text.Json.JsonValueKind.Null)
            return "";

        string? hint = null;
        if (step.Params.TryGetProperty("path", out var pathEl))
            hint = pathEl.GetString();
        else if (step.Params.TryGetProperty("command", out var cmdEl))
            hint = cmdEl.GetString();
        else if (step.Params.TryGetProperty("url", out var urlEl))
            hint = urlEl.GetString();
        else if (step.Params.TryGetProperty("text", out var textEl))
            hint = textEl.GetString();
        else if (step.Params.TryGetProperty("ms", out var msEl))
            hint = msEl.ToString() + "ms";
        else if (step.Params.TryGetProperty("from", out var fromEl))
            hint = fromEl.GetString();
        else if (step.Params.TryGetProperty("keys", out var keysEl))
            hint = keysEl.GetString();
        else if (step.Params.TryGetProperty("name", out var nameEl))
            hint = nameEl.GetString();
        else if (step.Params.TryGetProperty("level", out var levelEl))
            hint = levelEl.ToString();

        if (string.IsNullOrEmpty(hint)) return "";
        if (hint.Length > 50) hint = hint[..50] + "…";
        return $" <i>{WebUtility.HtmlEncode(hint)}</i>";
    }
}
