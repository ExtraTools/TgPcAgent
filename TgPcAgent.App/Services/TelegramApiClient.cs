using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TgPcAgent.App.Services;

public sealed class TelegramApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public TelegramApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(string token, long offset, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var payload = BuildPayload(
            ("offset", offset),
            ("timeout", timeoutSeconds),
            ("allowed_updates", new[] { "message", "callback_query" }));

        var updates = await PostAsync<List<TelegramUpdate>>(token, "getUpdates", payload, cancellationToken);
        return updates ?? [];
    }

    public Task SendMessageAsync(string token, long chatId, string text, object? replyMarkup, CancellationToken cancellationToken)
    {
        var payload = BuildPayload(
            ("chat_id", chatId),
            ("text", text),
            ("parse_mode", "HTML"),
            ("disable_web_page_preview", true),
            ("reply_markup", replyMarkup));

        return PostAsync<TelegramMessage>(token, "sendMessage", payload, cancellationToken);
    }

    public Task EditMessageReplyMarkupAsync(string token, long chatId, int messageId, object? replyMarkup, CancellationToken cancellationToken)
    {
        var payload = BuildPayload(
            ("chat_id", chatId),
            ("message_id", messageId),
            ("reply_markup", replyMarkup));

        return PostAsync<object>(token, "editMessageReplyMarkup", payload, cancellationToken);
    }

    public Task EditMessageTextAsync(
        string token,
        long chatId,
        int messageId,
        string text,
        object? replyMarkup,
        CancellationToken cancellationToken)
    {
        var payload = BuildPayload(
            ("chat_id", chatId),
            ("message_id", messageId),
            ("text", text),
            ("parse_mode", "HTML"),
            ("disable_web_page_preview", true),
            ("reply_markup", replyMarkup));

        return PostAsync<object>(token, "editMessageText", payload, cancellationToken);
    }

    public Task AnswerCallbackQueryAsync(string token, string callbackQueryId, string? text, CancellationToken cancellationToken)
    {
        var payload = BuildPayload(
            ("callback_query_id", callbackQueryId),
            ("text", text),
            ("show_alert", false));

        return PostAsync<object>(token, "answerCallbackQuery", payload, cancellationToken);
    }

    public async Task SendPhotoAsync(string token, long chatId, byte[] content, string fileName, string caption, CancellationToken cancellationToken)
    {
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(chatId.ToString()), "chat_id");
        formData.Add(new StringContent(caption, Encoding.UTF8), "caption");
        formData.Add(new StringContent("HTML"), "parse_mode");

        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        formData.Add(fileContent, "photo", fileName);

        using var response = await _httpClient.PostAsync(BuildMethodUri(token, "sendPhoto"), formData, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task SendDocumentAsync(string token, long chatId, byte[] content, string fileName, string caption, CancellationToken cancellationToken)
    {
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(chatId.ToString()), "chat_id");
        formData.Add(new StringContent(caption, Encoding.UTF8), "caption");
        formData.Add(new StringContent("HTML"), "parse_mode");

        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        formData.Add(fileContent, "document", fileName);

        using var response = await _httpClient.PostAsync(BuildMethodUri(token, "sendDocument"), formData, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<T?> PostAsync<T>(string token, string methodName, Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, _serializerOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildMethodUri(token, methodName))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadEnvelopeAsync<T>(response, cancellationToken);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await ReadEnvelopeAsync<object>(response, cancellationToken);
    }

    private async Task<T?> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var envelope = await JsonSerializer.DeserializeAsync<TelegramEnvelope<T>>(stream, _serializerOptions, cancellationToken);

        if (!response.IsSuccessStatusCode || envelope is null || !envelope.Ok)
        {
            throw new InvalidOperationException(envelope?.Description ?? $"Telegram API call failed with HTTP {(int)response.StatusCode}.");
        }

        return envelope.Result;
    }

    private static Uri BuildMethodUri(string token, string methodName)
    {
        return new Uri($"https://api.telegram.org/bot{token}/{methodName}");
    }

    private static Dictionary<string, object?> BuildPayload(params (string Key, object? Value)[] values)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (key, value) in values)
        {
            if (value is null)
            {
                continue;
            }

            payload[key] = value;
        }

        return payload;
    }
}
