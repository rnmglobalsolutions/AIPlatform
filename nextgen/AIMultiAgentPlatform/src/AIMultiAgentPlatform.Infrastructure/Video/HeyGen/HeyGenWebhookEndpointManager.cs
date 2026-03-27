using System.Net.Http;
using System.Text;
using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Video.HeyGen;

public sealed class HeyGenWebhookEndpointManager : IWebhookEndpointManager, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HeyGenOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HeyGenWebhookEndpointManager(HeyGenOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = NormalizeBaseAddress(options.BaseUrl);
        }
    }

    public async Task<WebhookEndpointListResult> ListAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return WebhookEndpointListResult.Failure("HeyGen integration is disabled in the current environment.");
        }

        if (!_options.HasRequiredConfiguration)
        {
            return WebhookEndpointListResult.Failure("HeyGen API key configuration is incomplete.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/webhook/endpoint.list");
        request.Headers.Add("X-Api-Key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return WebhookEndpointListResult.Failure($"HeyGen returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var data = document.RootElement.GetProperty("data");
            var endpoints = data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().Select(MapDescriptor).Where(static item => item is not null).Cast<WebhookEndpointDescriptor>().ToArray()
                : [];

            return WebhookEndpointListResult.Success(endpoints);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return WebhookEndpointListResult.Failure($"HeyGen list response could not be parsed: {exception.Message}");
        }
    }

    public async Task<WebhookEndpointMutationResult> CreateAsync(string url, IReadOnlyList<string> events, CancellationToken cancellationToken)
    {
        using var request = BuildMutationRequest(HttpMethod.Post, "v1/webhook/endpoint.add", new { url, events });
        return await SendMutationAsync(request, cancellationToken);
    }

    public async Task<WebhookEndpointMutationResult> UpdateAsync(string endpointId, string url, IReadOnlyList<string> events, CancellationToken cancellationToken)
    {
        using var request = BuildMutationRequest(HttpMethod.Patch, "v1/webhook/endpoint.update", new { endpoint_id = endpointId, url, events });
        return await SendMutationAsync(request, cancellationToken);
    }

    public async Task<WebhookEndpointDeletionResult> DeleteAsync(string endpointId, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return WebhookEndpointDeletionResult.Failure("HeyGen integration is disabled in the current environment.");
        }

        if (!_options.HasRequiredConfiguration)
        {
            return WebhookEndpointDeletionResult.Failure("HeyGen API key configuration is incomplete.");
        }

        using var request = BuildMutationRequest(HttpMethod.Delete, "v1/webhook/endpoint.delete", new { endpoint_id = endpointId });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return WebhookEndpointDeletionResult.Failure($"HeyGen returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        return WebhookEndpointDeletionResult.Success();
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<WebhookEndpointMutationResult> SendMutationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return WebhookEndpointMutationResult.Failure("HeyGen integration is disabled in the current environment.");
        }

        if (!_options.HasRequiredConfiguration)
        {
            return WebhookEndpointMutationResult.Failure("HeyGen API key configuration is incomplete.");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return WebhookEndpointMutationResult.Failure($"HeyGen returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var data = document.RootElement.GetProperty("data");
            var endpoint = MapDescriptor(data);

            return endpoint is null
                ? WebhookEndpointMutationResult.Failure("HeyGen mutation response did not include endpoint details.")
                : WebhookEndpointMutationResult.Success(endpoint);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return WebhookEndpointMutationResult.Failure($"HeyGen mutation response could not be parsed: {exception.Message}");
        }
    }

    private HttpRequestMessage BuildMutationRequest(HttpMethod method, string path, object payload)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Api-Key", _options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");
        return request;
    }

    private static WebhookEndpointDescriptor? MapDescriptor(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var endpointId = data.TryGetProperty("endpoint_id", out var endpointIdElement)
            ? endpointIdElement.GetString() ?? string.Empty
            : string.Empty;
        var url = data.TryGetProperty("url", out var urlElement)
            ? urlElement.GetString() ?? string.Empty
            : string.Empty;
        var status = data.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? string.Empty
            : string.Empty;
        var secret = data.TryGetProperty("secret", out var secretElement)
            ? secretElement.GetString() ?? string.Empty
            : string.Empty;
        DateTime? createdUtc = data.TryGetProperty("created_at", out var createdAtElement) &&
                               DateTime.TryParse(createdAtElement.GetString(), out var createdAt)
            ? createdAt
            : null;

        var events = data.TryGetProperty("events", out var eventsElement) && eventsElement.ValueKind == JsonValueKind.Array
            ? eventsElement.EnumerateArray()
                .Select(static item => item.GetString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray()
            : [];

        if (string.IsNullOrWhiteSpace(endpointId) || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return new WebhookEndpointDescriptor("HeyGen", endpointId, url, status, events, secret, createdUtc);
    }

    private static Uri NormalizeBaseAddress(string baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl) ? HeyGenOptions.Default.BaseUrl : baseUrl.Trim();
        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        return new Uri(normalized, UriKind.Absolute);
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
}
