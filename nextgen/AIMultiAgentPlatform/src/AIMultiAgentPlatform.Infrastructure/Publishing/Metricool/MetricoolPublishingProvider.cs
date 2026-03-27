using System.Net.Http;
using System.Text;
using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Publishing.Metricool;

public sealed class MetricoolPublishingProvider : IPublishingProvider, IDisposable
{
    private readonly MetricoolOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public MetricoolPublishingProvider(MetricoolOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = NormalizeBaseAddress(options.BaseUrl);
        }
    }

    public string ProviderName => "Metricool";

    public async Task<PublishingResult> PublishAsync(PublishingRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return PublishingResult.Failure(request.Platform, "Metricool publishing is disabled in the current environment.");
        }

        if (!_options.HasRequiredConfiguration)
        {
            return PublishingResult.Failure(request.Platform, "Metricool publishing configuration is incomplete.");
        }

        if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.ExternalProfileId))
        {
            return PublishingResult.Failure(request.Platform, "Metricool publishing requires an access token and external profile id.");
        }

        var requestUri = BuildRequestUri(
            _options.PublishPathTemplate,
            request.Platform,
            request.ExternalProfileId,
            string.Empty,
            request.AccessToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
        ApplyAuthentication(httpRequest, request.AccessToken);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(BuildPublishPayload(request)),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return PublishingResult.Failure(request.Platform, $"Metricool returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;
            if (TryExtractFailure(root, out var failureReason))
            {
                return PublishingResult.Failure(request.Platform, failureReason);
            }

            var externalPostId = ReadFirstString(root, "externalPostId", "postId", "publicationId", "id");
            if (string.IsNullOrWhiteSpace(externalPostId))
            {
                return PublishingResult.Failure(request.Platform, "Metricool publish response did not include a post identifier.");
            }

            var externalUrl = ReadFirstString(root, "externalUrl", "permalink", "link", "url");
            return PublishingResult.Success(request.Platform, externalPostId, externalUrl);
        }
        catch (JsonException exception)
        {
            return PublishingResult.Failure(request.Platform, $"Metricool publish response could not be parsed: {exception.Message}");
        }
    }

    public async Task<PublishingReconciliationResult> ReconcileAsync(PublishingReconciliationRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return PublishingReconciliationResult.Failure(request.Platform, "Metricool publishing is disabled in the current environment.");
        }

        if (!_options.HasRequiredConfiguration)
        {
            return PublishingReconciliationResult.Failure(request.Platform, "Metricool publishing configuration is incomplete.");
        }

        if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.ExternalPostId))
        {
            return PublishingReconciliationResult.Failure(request.Platform, "Metricool reconciliation requires an access token and external post id.");
        }

        var requestUri = BuildRequestUri(
            _options.ReconcilePathTemplate,
            request.Platform,
            request.ExternalProfileId,
            request.ExternalPostId,
            request.AccessToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        ApplyAuthentication(httpRequest, request.AccessToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return PublishingReconciliationResult.Failure(request.Platform, $"Metricool returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;
            if (TryExtractFailure(root, out var failureReason))
            {
                return PublishingReconciliationResult.Failure(request.Platform, failureReason);
            }

            var providerStatus = ReadFirstString(root, "providerStatus", "publicationStatus", "status", "state");
            var externalUrl = ReadFirstString(root, "externalUrl", "permalink", "link", "url");
            var publishedUtc = ReadFirstDateTime(root, "publishedAt", "published_at", "publishedUtc", "postedAt", "scheduledAt", "scheduled_at");
            var metricsSource = FindFirstMetricsContainer(root) ?? root;
            var metrics = new PublishingMetrics(
                ReadFirstLong(metricsSource, "reach", "impressions", "views"),
                ReadFirstLong(metricsSource, "clicks", "linkClicks", "websiteClicks"),
                ReadFirstLong(metricsSource, "likes", "favorites", "reactions"),
                ReadFirstLong(metricsSource, "comments", "replies"),
                ReadFirstLong(metricsSource, "shares", "reposts", "retweets"));

            return PublishingReconciliationResult.Success(
                request.Platform,
                string.IsNullOrWhiteSpace(providerStatus) ? "published" : providerStatus,
                string.IsNullOrWhiteSpace(externalUrl) ? request.ExistingExternalUrl : externalUrl,
                metrics,
                publishedUtc);
        }
        catch (JsonException exception)
        {
            return PublishingReconciliationResult.Failure(request.Platform, $"Metricool reconciliation response could not be parsed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private object BuildPublishPayload(PublishingRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["externalProfileId"] = request.ExternalProfileId,
            ["platform"] = request.Platform,
            ["text"] = request.Caption,
            ["publishAtUtc"] = request.PublishAtUtc.ToString("O"),
            ["tenantId"] = request.TenantId,
            ["correlationId"] = request.CorrelationId
        };

        if (!string.IsNullOrWhiteSpace(request.AssetUrl))
        {
            payload["mediaUrl"] = request.AssetUrl;
        }

        if (request.Metadata is not null && request.Metadata.Count > 0)
        {
            payload["metadata"] = request.Metadata;
        }

        return payload;
    }

    private string BuildRequestUri(
        string pathTemplate,
        string platform,
        string externalProfileId,
        string externalPostId,
        string accessToken)
    {
        var resolvedPath = pathTemplate
            .Replace("{platform}", Uri.EscapeDataString(platform ?? string.Empty), StringComparison.Ordinal)
            .Replace("{externalProfileId}", Uri.EscapeDataString(externalProfileId ?? string.Empty), StringComparison.Ordinal)
            .Replace("{externalPostId}", Uri.EscapeDataString(externalPostId ?? string.Empty), StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(_options.AccessTokenQueryParameterName))
        {
            return resolvedPath;
        }

        var separator = resolvedPath.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{resolvedPath}{separator}{Uri.EscapeDataString(_options.AccessTokenQueryParameterName)}={Uri.EscapeDataString(accessToken)}";
    }

    private void ApplyAuthentication(HttpRequestMessage request, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessTokenHeaderName))
        {
            return;
        }

        var headerValue = string.IsNullOrWhiteSpace(_options.AccessTokenHeaderPrefix)
            ? accessToken
            : $"{_options.AccessTokenHeaderPrefix} {accessToken}";

        request.Headers.Remove(_options.AccessTokenHeaderName);
        request.Headers.TryAddWithoutValidation(_options.AccessTokenHeaderName, headerValue);
    }

    private static bool TryExtractFailure(JsonElement root, out string failureReason)
    {
        if (TryReadProperty(root, "success", out var successElement) &&
            successElement.ValueKind == JsonValueKind.False)
        {
            failureReason = ReadFirstString(root, "message", "error", "detail");
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                failureReason = "Metricool did not accept the request.";
            }

            return true;
        }

        if (TryReadProperty(root, "ok", out var okElement) &&
            okElement.ValueKind == JsonValueKind.False)
        {
            failureReason = ReadFirstString(root, "message", "error", "detail");
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                failureReason = "Metricool did not accept the request.";
            }

            return true;
        }

        failureReason = string.Empty;
        return false;
    }

    private static JsonElement? FindFirstMetricsContainer(JsonElement root)
    {
        foreach (var candidate in new[] { "metrics", "statistics", "stats", "analytics", "insights" })
        {
            if (TryReadProperty(root, candidate, out var value) && value.ValueKind == JsonValueKind.Object)
            {
                return value;
            }
        }

        return null;
    }

    private static string ReadFirstString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryReadProperty(root, propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? string.Empty;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
                {
                    return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        return string.Empty;
    }

    private static long ReadFirstLong(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryReadProperty(root, propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
                {
                    return number;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    long.TryParse(value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return 0;
    }

    private static DateTime? ReadFirstDateTime(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryReadProperty(root, propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(
                    value.GetString(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        return null;
    }

    private static bool TryReadProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out value))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryReadProperty(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryReadProperty(item, propertyName, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static Uri NormalizeBaseAddress(string baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl) ? MetricoolOptions.Default.BaseUrl : baseUrl.Trim();
        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        return new Uri(normalized, UriKind.Absolute);
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= maxLength ? value : value[..maxLength];
}
