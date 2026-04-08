using System.Net.Http.Json;
using System.Reflection;

namespace TgPcAgent.App.Services;

public sealed class CloudRelayService : IDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);

    private readonly ConfigurationService _configurationService;
    private readonly FileLogger _logger;
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    public CloudRelayService(ConfigurationService configurationService, FileLogger logger)
    {
        _configurationService = configurationService;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public void Start()
    {
        if (_loopTask is not null)
        {
            return;
        }

        if (!TryGetSettings(out _))
        {
            _logger.Info("Cloud relay disabled: missing URL or secret.");
            return;
        }

        _loopTask = RunAsync(_cts.Token);
    }

    public async Task SendShutdownAsync(TimeSpan timeout)
    {
        if (!TryGetSettings(out var settings))
        {
            return;
        }

        using var timeoutCts = new CancellationTokenSource(timeout);

        try
        {
            await PostJsonAsync(
                settings,
                "api/agent/shutdown",
                new
                {
                    secret = settings.Secret,
                    machineName = Environment.MachineName,
                    reason = "agent-exit"
                },
                timeoutCts.Token);
        }
        catch (Exception exception)
        {
            _logger.Info($"Cloud shutdown relay failed: {exception.Message}");
        }
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
            // Best effort cancellation only.
        }

        _cts.Dispose();
        _httpClient.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SendHeartbeatAsync(cancellationToken);

            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await SendHeartbeatAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            _logger.Info($"Cloud relay loop failed: {exception.Message}");
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (!TryGetSettings(out var settings))
        {
            return;
        }

        try
        {
            var config = _configurationService.GetSnapshot();
            var agentSecret = _configurationService.GetAgentSecret(config);
            await PostJsonAsync(
                settings,
                "api/agent/heartbeat",
                new
                {
                    secret = agentSecret ?? settings.Secret,
                    agentId = config.AgentId,
                    machineName = Environment.MachineName,
                    heartbeatId = Guid.NewGuid().ToString("N"),
                    appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.Info($"Cloud heartbeat failed: {exception.Message}");
        }
    }

    private async Task PostJsonAsync(CloudRelaySettings settings, string relativePath, object payload, CancellationToken cancellationToken)
    {
        var requestUri = new Uri(settings.BaseUri, relativePath);
        using var response = await _httpClient.PostAsJsonAsync(requestUri, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private bool TryGetSettings(out CloudRelaySettings settings)
    {
        var config = _configurationService.GetSnapshot();
        var baseUrl = config.CloudRelayUrl?.Trim();
        var secret = _configurationService.GetCloudAgentSecret(config)?.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            settings = default;
            return false;
        }

        if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute, out var baseUri))
        {
            settings = default;
            return false;
        }

        settings = new CloudRelaySettings(baseUri, secret);
        return true;
    }

    private readonly record struct CloudRelaySettings(Uri BaseUri, string Secret);
}
