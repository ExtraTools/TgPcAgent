using TgPcAgent.App.Models;
using TgPcAgent.Core.Automation;

namespace TgPcAgent.App.Services;

public sealed class AutomationService : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

    private readonly ConfigurationService _configurationService;
    private readonly CommandExecutionService _commandExecutionService;
    private readonly FileLogger _logger;
    private readonly CancellationTokenSource _cts = new();

    private Task? _loopTask;
    private int _lastPingIntervalMinutes = -1;
    private int _lastScreenshotIntervalMinutes = -1;
    private DateTimeOffset? _nextPingAtUtc;
    private DateTimeOffset? _nextScreenshotAtUtc;

    public AutomationService(
        ConfigurationService configurationService,
        CommandExecutionService commandExecutionService,
        FileLogger logger)
    {
        _configurationService = configurationService;
        _commandExecutionService = commandExecutionService;
        _logger = logger;
    }

    public void Start()
    {
        if (_loopTask is not null)
        {
            return;
        }

        _loopTask = RunAsync(_cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _loopTask?.GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort shutdown only.
        }

        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteDueJobsAsync(cancellationToken);

            using var timer = new PeriodicTimer(TickInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ExecuteDueJobsAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            _logger.Error("Automation loop failed.", exception);
        }
    }

    private async Task ExecuteDueJobsAsync(CancellationToken cancellationToken)
    {
        var config = _configurationService.GetSnapshot();
        var nowUtc = DateTimeOffset.UtcNow;

        ApplyScheduleChange(config.AutoPingIntervalMinutes, ref _lastPingIntervalMinutes, ref _nextPingAtUtc, nowUtc);
        ApplyScheduleChange(config.AutoScreenshotIntervalMinutes, ref _lastScreenshotIntervalMinutes, ref _nextScreenshotAtUtc, nowUtc);

        if (IsDue(config, _nextPingAtUtc, nowUtc))
        {
            await TryRunAsync(() => _commandExecutionService.SendAutoPingAsync(cancellationToken), "auto ping", cancellationToken);
            _nextPingAtUtc = nowUtc.AddMinutes(config.AutoPingIntervalMinutes);
        }

        if (IsDue(config, _nextScreenshotAtUtc, nowUtc))
        {
            await TryRunAsync(() => _commandExecutionService.SendAutoScreenshotAsync(cancellationToken), "auto screenshot", cancellationToken);
            _nextScreenshotAtUtc = nowUtc.AddMinutes(config.AutoScreenshotIntervalMinutes);
        }
    }

    private static bool IsDue(AppConfig config, DateTimeOffset? nextDueAtUtc, DateTimeOffset nowUtc)
    {
        return config.OwnerChatId.HasValue &&
               nextDueAtUtc.HasValue &&
               nowUtc >= nextDueAtUtc.Value;
    }

    private static void ApplyScheduleChange(int requestedMinutes, ref int previousMinutes, ref DateTimeOffset? nextDueAtUtc, DateTimeOffset nowUtc)
    {
        var effectiveMinutes = AutomationIntervalCatalog.IsSupported(requestedMinutes) ? requestedMinutes : 0;
        if (effectiveMinutes == previousMinutes)
        {
            return;
        }

        previousMinutes = effectiveMinutes;
        nextDueAtUtc = effectiveMinutes > 0
            ? nowUtc.AddMinutes(effectiveMinutes)
            : null;
    }

    private async Task TryRunAsync(Func<Task> operation, string operationName, CancellationToken cancellationToken)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.Error($"Failed to run {operationName}.", exception);
        }
    }
}
