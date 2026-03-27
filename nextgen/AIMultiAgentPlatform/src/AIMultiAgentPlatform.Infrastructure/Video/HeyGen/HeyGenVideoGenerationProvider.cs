using System.Net.Http;
using System.Text;
using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Video.HeyGen;

public sealed class HeyGenVideoGenerationProvider : IVideoGenerationProvider, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HeyGenOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HeyGenVideoGenerationProvider(HeyGenOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = NormalizeBaseAddress(options.BaseUrl);
        }
    }

    public string ProviderName => "HeyGen";

    public async Task<VideoGenerationSubmissionResult> SubmitAsync(VideoGenerationRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return VideoGenerationSubmissionResult.Rejected(ProviderName, "HeyGen integration is disabled in the current environment.");
        }

        if (!_options.HasRequiredConfiguration)
        {
            return VideoGenerationSubmissionResult.Rejected(ProviderName, "HeyGen API key configuration is incomplete.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/video_agent/generate");
        httpRequest.Headers.Add("X-API-KEY", _options.ApiKey);
        httpRequest.Content = new StringContent(BuildSubmitPayloadJson(request), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return VideoGenerationSubmissionResult.Rejected(
                ProviderName,
                $"HeyGen returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                return VideoGenerationSubmissionResult.Rejected(
                    ProviderName,
                    errorElement.GetString() ?? "HeyGen returned an unknown error.");
            }

            var videoId = root
                .GetProperty("data")
                .GetProperty("video_id")
                .GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(videoId))
            {
                return VideoGenerationSubmissionResult.Rejected(ProviderName, "HeyGen returned an empty video_id.");
            }

            return VideoGenerationSubmissionResult.Accepted(videoId, ProviderName, "submitted");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return VideoGenerationSubmissionResult.Rejected(
                ProviderName,
                $"HeyGen response could not be parsed: {exception.Message}");
        }
    }

    public async Task<VideoGenerationStatusResult> GetStatusAsync(string providerJobId, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new VideoGenerationStatusResult(providerJobId, ProviderName, "failed", string.Empty, string.Empty, "HeyGen integration is disabled in the current environment.");
        }

        if (!_options.HasRequiredConfiguration)
        {
            return new VideoGenerationStatusResult(providerJobId, ProviderName, "failed", string.Empty, string.Empty, "HeyGen API key configuration is incomplete.");
        }

        var requestUri = $"v1/video_status.get?video_id={Uri.EscapeDataString(providerJobId)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        httpRequest.Headers.Add("X-API-KEY", _options.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new VideoGenerationStatusResult(
                providerJobId,
                ProviderName,
                "failed",
                string.Empty,
                string.Empty,
                $"HeyGen returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;
            var data = root.GetProperty("data");
            var status = data.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? string.Empty
                : string.Empty;
            var videoUrl = data.TryGetProperty("video_url", out var videoUrlElement)
                ? videoUrlElement.GetString() ?? string.Empty
                : string.Empty;
            var captionUrl = data.TryGetProperty("caption_url", out var captionUrlElement)
                ? captionUrlElement.GetString() ?? string.Empty
                : string.Empty;
            var failureReason = ExtractFailureReason(data);
            var captionData = await TryDownloadCaptionDataAsync(captionUrl, cancellationToken);

            return new VideoGenerationStatusResult(
                providerJobId,
                ProviderName,
                status,
                videoUrl,
                captionData.TranscriptText,
                failureReason,
                captionData.TranscriptSegments);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return new VideoGenerationStatusResult(
                providerJobId,
                ProviderName,
                "failed",
                string.Empty,
                string.Empty,
                $"HeyGen status response could not be parsed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private string BuildSubmitPayloadJson(VideoGenerationRequest request)
    {
        var payload = new
        {
            prompt = BuildPrompt(request),
            config = new
            {
                avatar_id = string.IsNullOrWhiteSpace(_options.DefaultAvatarId) ? null : _options.DefaultAvatarId,
                duration_sec = _options.DefaultDurationSeconds,
                orientation = request.AspectRatio == "16:9" ? "landscape" : "portrait"
            },
            callback_id = request.CorrelationId
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static string BuildPrompt(VideoGenerationRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("Create a concise avatar video. ");
        sb.Append("Use this title as context: ").Append(request.Title).Append(". ");
        sb.Append("Speak in ").Append(request.Language).Append(". ");
        sb.Append("Follow this production-ready script exactly, with natural conversational pacing: ");
        sb.Append(request.Script);
        return sb.ToString();
    }

    private async Task<CaptionDownloadResult> TryDownloadCaptionDataAsync(string captionUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(captionUrl))
        {
            return new CaptionDownloadResult(string.Empty, Array.Empty<TimedTranscriptSegment>());
        }

        try
        {
            using var response = await _httpClient.GetAsync(captionUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new CaptionDownloadResult(string.Empty, Array.Empty<TimedTranscriptSegment>());
            }

            var captionText = await response.Content.ReadAsStringAsync(cancellationToken);
            var segments = ParseTimedTranscriptSegments(captionText);
            var transcriptText = segments.Count > 0
                ? string.Join(" ", segments.Select(static segment => segment.Text))
                : NormalizeCaptionLine(captionText);

            return new CaptionDownloadResult(transcriptText, segments);
        }
        catch (Exception)
        {
            return new CaptionDownloadResult(string.Empty, Array.Empty<TimedTranscriptSegment>());
        }
    }

    private static IReadOnlyList<TimedTranscriptSegment> ParseTimedTranscriptSegments(string captionText)
    {
        if (string.IsNullOrWhiteSpace(captionText))
        {
            return Array.Empty<TimedTranscriptSegment>();
        }

        return captionText.Contains("Dialogue:", StringComparison.OrdinalIgnoreCase)
            ? ParseAssSegments(captionText)
            : ParseSrtOrVttSegments(captionText);
    }

    private static IReadOnlyList<TimedTranscriptSegment> ParseAssSegments(string captionText)
    {
        var segments = new List<TimedTranscriptSegment>();
        foreach (var line in captionText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line["Dialogue:".Length..].Trim();
            var parts = payload.Split(',', 10);
            if (parts.Length < 10 ||
                !TryParseTimestamp(parts[1], out var startSeconds) ||
                !TryParseTimestamp(parts[2], out var endSeconds))
            {
                continue;
            }

            var text = NormalizeCaptionLine(parts[9]);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            segments.Add(new TimedTranscriptSegment(startSeconds, endSeconds, text));
        }

        return segments;
    }

    private static IReadOnlyList<TimedTranscriptSegment> ParseSrtOrVttSegments(string captionText)
    {
        var segments = new List<TimedTranscriptSegment>();
        var blocks = captionText.Replace("\r\n", "\n", StringComparison.Ordinal).Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(static line => line.Trim())
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            if (lines.Length == 0)
            {
                continue;
            }

            var timestampLineIndex = Array.FindIndex(lines, static line => line.Contains("-->", StringComparison.Ordinal));
            if (timestampLineIndex < 0)
            {
                continue;
            }

            var timestampParts = lines[timestampLineIndex].Split("-->", 2, StringSplitOptions.TrimEntries);
            if (timestampParts.Length != 2 ||
                !TryParseTimestamp(timestampParts[0], out var startSeconds) ||
                !TryParseTimestamp(timestampParts[1], out var endSeconds))
            {
                continue;
            }

            var text = NormalizeCaptionLine(string.Join(" ", lines[(timestampLineIndex + 1)..]));
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            segments.Add(new TimedTranscriptSegment(startSeconds, endSeconds, text));
        }

        return segments;
    }

    private static bool TryParseTimestamp(string rawValue, out double seconds)
    {
        seconds = 0d;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var value = rawValue.Trim();
        var trailingSpaceIndex = value.IndexOf(' ');
        if (trailingSpaceIndex >= 0)
        {
            value = value[..trailingSpaceIndex];
        }

        value = value.Replace(',', '.');
        if (!TimeSpan.TryParse(value, out var timeSpan))
        {
            return false;
        }

        seconds = Math.Round(timeSpan.TotalSeconds, 3);
        return true;
    }

    private static string NormalizeCaptionLine(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var normalized = rawText
            .Replace("\\N", " ", StringComparison.Ordinal)
            .Replace("\\n", " ", StringComparison.Ordinal);

        var builder = new StringBuilder(normalized.Length);
        var insideBraceTag = false;
        foreach (var character in normalized)
        {
            if (character == '{')
            {
                insideBraceTag = true;
                continue;
            }

            if (character == '}')
            {
                insideBraceTag = false;
                continue;
            }

            if (!insideBraceTag)
            {
                builder.Append(character);
            }
        }

        return string.Join(
            " ",
            builder.ToString()
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ExtractFailureReason(JsonElement data)
    {
        if (!data.TryGetProperty("error", out var errorElement) ||
            errorElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        if (errorElement.ValueKind == JsonValueKind.String)
        {
            return errorElement.GetString() ?? string.Empty;
        }

        if (errorElement.ValueKind == JsonValueKind.Object)
        {
            var message = errorElement.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : string.Empty;
            var detail = errorElement.TryGetProperty("detail", out var detailElement)
                ? detailElement.GetString()
                : string.Empty;

            return string.Join(" ", new[] { message, detail }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        return string.Empty;
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

    private sealed record CaptionDownloadResult(
        string TranscriptText,
        IReadOnlyList<TimedTranscriptSegment> TranscriptSegments);
}
