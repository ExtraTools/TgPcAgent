using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

/// <summary>
/// Polls the cloud relay for pending commands, replacing the old TelegramPollingService.
/// Commands are fetched from the cloud queue, processed locally, and results sent back.
/// </summary>
public sealed class CloudCommandPollingService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(30);
    private const double MaxBackoffSeconds = 60;

    private readonly ConfigurationService _configurationService;
    private readonly CloudApiClient _cloudApiClient;
    private readonly Func<CloudCommand, CancellationToken, Task> _commandHandler;
    private readonly FileLogger _logger;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public CloudCommandPollingService(
        ConfigurationService configurationService,
        CloudApiClient cloudApiClient,
        Func<CloudCommand, CancellationToken, Task> commandHandler,
        FileLogger logger)
    {
        _configurationService = configurationService;
        _cloudApiClient = cloudApiClient;
        _commandHandler = commandHandler;
        _logger = logger;
    }

    public void Start()
    {
        if (_loopTask is not null) return;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null || _loopTask is null) return;
        _cts.Cancel();
        try { await _loopTask; }
        catch (OperationCanceledException) { }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    public void Dispose()
    {
        if (_loopTask is not null) StopAsync().GetAwaiter().GetResult();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        int consecutiveErrors = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var config = _configurationService.GetSnapshot();
                var baseUrl = config.CloudRelayUrl?.Trim();
                var agentId = config.AgentId;
                var secret = _configurationService.GetAgentSecret(config);

                if (string.IsNullOrWhiteSpace(baseUrl) ||
                    string.IsNullOrWhiteSpace(agentId) ||
                    string.IsNullOrWhiteSpace(secret))
                {
                    await Task.Delay(ErrorDelay, cancellationToken);
                    continue;
                }

                if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute, out var baseUri))
                {
                    await Task.Delay(ErrorDelay, cancellationToken);
                    continue;
                }

                var commands = await _cloudApiClient.PollCommandsAsync(baseUri, agentId, secret, cancellationToken);

                if (commands == null)
                {
                    _logger.Info("Cloud poll returned 401. Re-registering agent...");
                    var regResult = await _cloudApiClient.RegisterAsync(baseUri, agentId, secret,
                        Environment.MachineName, cancellationToken);
                    _logger.Info(regResult != null
                        ? $"Re-registration OK (paired={regResult.AlreadyPaired})"
                        : "Re-registration failed");
                    await Task.Delay(ErrorDelay, cancellationToken);
                    continue;
                }

                if (commands.Count == 0 && consecutiveErrors == 0)
                {
                    await Task.Delay(PollInterval, cancellationToken);
                    continue;
                }

                consecutiveErrors = 0;

                foreach (var command in commands)
                {
                    try
                    {
                        await _commandHandler(command, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Failed to handle cloud command {command.Id} ({command.Type}).", ex);
                    }
                }

                await Task.Delay(commands.Count > 0 ? TimeSpan.FromMilliseconds(500) : PollInterval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                var backoff = TimeSpan.FromSeconds(Math.Min(5 * Math.Pow(2, consecutiveErrors - 1), MaxBackoffSeconds));
                _logger.Error($"Cloud poll failed (attempt {consecutiveErrors}, next retry in {backoff.TotalSeconds:F0}s).", ex);
                await Task.Delay(backoff, cancellationToken);
            }
        }
    }
}
