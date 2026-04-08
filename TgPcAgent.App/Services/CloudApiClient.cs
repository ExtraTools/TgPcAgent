using System.Net.Http.Json;
using System.Text.Json;
using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

/// <summary>
/// HTTP client for communicating with the cloud relay API.
/// </summary>
public sealed class CloudApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly FileLogger _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public CloudApiClient(FileLogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    /// <summary>
    /// Register this agent with the cloud, getting a pairing code back.
    /// </summary>
    public async Task<CloudRegisterResponse?> RegisterAsync(Uri baseUri, string agentId, string secret, string machineName, CancellationToken cancellationToken, bool forceRePair = false)
    {
        var requestUri = new Uri(baseUri, "api/agent/register");
        var payload = new
        {
            agentId,
            secret,
            machineName,
            forceRePair
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(requestUri, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CloudRegisterResponse>(_serializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error("Cloud register failed.", ex);
            return null;
        }
    }

    /// <summary>
    /// Poll the cloud for pending commands.
    /// </summary>
    public async Task<IReadOnlyList<CloudCommand>?> PollCommandsAsync(Uri baseUri, string agentId, string secret, CancellationToken cancellationToken)
    {
        var requestUri = new Uri(baseUri, "api/agent/commands");

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("X-Agent-Id", agentId);
        request.Headers.Add("X-Agent-Secret", secret);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return null;
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CloudCommandsResponse>(_serializerOptions, cancellationToken);
            return result?.Commands ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error("Cloud command poll failed.", ex);
            return [];
        }
    }

    /// <summary>
    /// Send a command result back to the cloud for delivery to Telegram.
    /// </summary>
    public async Task<CloudResultResponse?> SendResultAsync(Uri baseUri, CloudCommandResult result, CancellationToken cancellationToken)
    {
        var requestUri = new Uri(baseUri, "api/agent/result");

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(requestUri, result, _serializerOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CloudResultResponse>(_serializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error($"Cloud result delivery failed for command {result.CommandId}.", ex);
            return null;
        }
    }

    /// <summary>
    /// Request a fresh pairing code from cloud (re-registration).
    /// </summary>
    public async Task<string?> RefreshPairingCodeAsync(Uri baseUri, string agentId, string secret, string machineName, CancellationToken cancellationToken)
    {
        var result = await RegisterAsync(baseUri, agentId, secret, machineName, cancellationToken);
        return result?.PairingCode;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public sealed class CloudRegisterResponse
{
    public bool Ok { get; set; }
    public bool Registered { get; set; }
    public bool Updated { get; set; }
    public bool AlreadyPaired { get; set; }
    public string? PairingCode { get; set; }
    public long? OwnerChatId { get; set; }
}

public sealed class CloudCommandsResponse
{
    public bool Ok { get; set; }
    public List<CloudCommand>? Commands { get; set; }
}

public sealed class CloudResultResponse
{
    public bool Ok { get; set; }
    public int? MessageId { get; set; }
}
