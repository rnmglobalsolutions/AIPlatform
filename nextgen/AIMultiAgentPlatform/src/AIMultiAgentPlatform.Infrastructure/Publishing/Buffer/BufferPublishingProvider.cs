using System.Net.Http;
using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Publishing.Buffer;

public sealed class BufferPublishingProvider : IPublishingProvider, IDisposable
{
    private readonly BufferOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public BufferPublishingProvider(BufferOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = NormalizeBaseAddress(options.BaseUrl);
        }
    }

    public string ProviderName => "Buffer";

    public async Task<PublishingResult> PublishAsync(PublishingRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return PublishingResult.Failure(request.Platform, "Buffer publishing is disabled in the current environment.");
        }

        if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.ExternalProfileId))
        {
            return PublishingResult.Failure(request.Platform, "Buffer publishing requires an access token and external profile id.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "updates/create.json");
        httpRequest.Content = BuildFormContent(request);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return PublishingResult.Failure(request.Platform, $"Buffer returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;
            if (root.TryGetProperty("success", out var successElement) && successElement.ValueKind == JsonValueKind.False)
            {
                return PublishingResult.Failure(request.Platform, ExtractFailure(root));
            }

            var updates = root.TryGetProperty("updates", out var updatesElement) && updatesElement.ValueKind == JsonValueKind.Array
                ? updatesElement.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();
            var update = updates.FirstOrDefault();
            var externalPostId = update.ValueKind != JsonValueKind.Undefined && update.TryGetProperty("id", out var idElement)
                ? idElement.GetString() ?? string.Empty
                : string.Empty;
            var serviceUpdateId = update.ValueKind != JsonValueKind.Undefined && update.TryGetProperty("service_update_id", out var serviceUpdateIdElement)
                ? serviceUpdateIdElement.GetString() ?? string.Empty
                : string.Empty;

            return PublishingResult.Success(
                request.Platform,
                string.IsNullOrWhiteSpace(serviceUpdateId) ? externalPostId : serviceUpdateId,
                string.Empty);
        }
        catch (JsonException exception)
        {
            return PublishingResult.Failure(request.Platform, $"Buffer response could not be parsed: {exception.Message}");
        }
    }

    public async Task<PublishingReconciliationResult> ReconcileAsync(PublishingReconciliationRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return PublishingReconciliationResult.Failure(request.Platform, "Buffer publishing is disabled in the current environment.");
        }

        if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.ExternalPostId))
        {
            return PublishingReconciliationResult.Failure(request.Platform, "Buffer reconciliation requires an access token and external post id.");
        }

        var requestUri = $"updates/{Uri.EscapeDataString(request.ExternalPostId)}.json?access_token={Uri.EscapeDataString(request.AccessToken)}";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return PublishingReconciliationResult.Failure(request.Platform, $"Buffer returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;
            var providerStatus = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? string.Empty
                : string.Empty;
            var externalUrl = root.TryGetProperty("service_update_id", out var serviceUpdateIdElement) && !string.IsNullOrWhiteSpace(serviceUpdateIdElement.GetString())
                ? serviceUpdateIdElement.GetString()!
                : request.ExistingExternalUrl;
            var metrics = ParseMetrics(root);
            DateTime? publishedUtc = root.TryGetProperty("sent_at", out var sentAtElement) && sentAtElement.TryGetInt64(out var sentAtUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(sentAtUnix).UtcDateTime
                : null;

            return PublishingReconciliationResult.Success(
                request.Platform,
                providerStatus,
                externalUrl,
                metrics,
                publishedUtc);
        }
        catch (JsonException exception)
        {
            return PublishingReconciliationResult.Failure(request.Platform, $"Buffer reconciliation response could not be parsed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static FormUrlEncodedContent BuildFormContent(PublishingRequest request)
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("access_token", request.AccessToken),
            new("profile_ids[]", request.ExternalProfileId),
            new("text", request.Caption),
            new("scheduled_at", request.PublishAtUtc.ToString("O"))
        };

        if (!string.IsNullOrWhiteSpace(request.AssetUrl))
        {
            values.Add(new KeyValuePair<string, string>("media[photo]", request.AssetUrl));
        }

        return new FormUrlEncodedContent(values);
    }

    private static string ExtractFailure(JsonElement root)
    {
        if (root.TryGetProperty("message", out var messageElement) && !string.IsNullOrWhiteSpace(messageElement.GetString()))
        {
            return messageElement.GetString()!;
        }

        if (root.TryGetProperty("error", out var errorElement) && !string.IsNullOrWhiteSpace(errorElement.GetString()))
        {
            return errorElement.GetString()!;
        }

        return "Buffer did not accept the publishing request.";
    }

    private static Uri NormalizeBaseAddress(string baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl) ? BufferOptions.Default.BaseUrl : baseUrl.Trim();
        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        return new Uri(normalized, UriKind.Absolute);
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= maxLength ? value : value[..maxLength];

    private static PublishingMetrics ParseMetrics(JsonElement root)
    {
        if (!root.TryGetProperty("statistics", out var statistics) || statistics.ValueKind != JsonValueKind.Object)
        {
            return new PublishingMetrics(0, 0, 0, 0, 0);
        }

        return new PublishingMetrics(
            ReadLong(statistics, "reach"),
            ReadLong(statistics, "clicks"),
            Math.Max(ReadLong(statistics, "likes"), ReadLong(statistics, "favorites")),
            Math.Max(ReadLong(statistics, "comments"), ReadLong(statistics, "mentions")),
            Math.Max(ReadLong(statistics, "shares"), ReadLong(statistics, "retweets")));
    }

    private static long ReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }
}
