using System.Net.Http;
using System.Text;
using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.AI.OpenAi;

public sealed class OpenAiContentGenerator : ILLMContentGenerator, IDisposable
{
    private const string CanonicalContentFrameSchema = """
        {
          "type": "object",
          "properties": {
            "primaryHook": { "type": "string" },
            "hookVariants": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "label": { "type": "string" },
                  "text": { "type": "string" }
                },
                "required": ["label", "text"],
                "additionalProperties": false
              }
            },
            "coreMessage": { "type": "string" },
            "body": { "type": "string" },
            "payoff": { "type": "string" },
            "callToAction": { "type": "string" },
            "engagementPrompt": { "type": "string" },
            "desiredActionPrompt": { "type": "string" },
            "languageGuidance": { "type": "string" },
            "languageFormatInstruction": { "type": "string" },
            "productionNotes": { "type": "string" },
            "repurposeDirectives": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "format": { "type": "string" },
                  "intent": { "type": "string" },
                  "prompt": { "type": "string" }
                },
                "required": ["format", "intent", "prompt"],
                "additionalProperties": false
              }
            }
          },
          "required": [
            "primaryHook",
            "hookVariants",
            "coreMessage",
            "body",
            "payoff",
            "callToAction",
            "engagementPrompt",
            "desiredActionPrompt",
            "languageGuidance",
            "languageFormatInstruction",
            "productionNotes",
            "repurposeDirectives"
          ],
          "additionalProperties": false
        }
        """;
    private static readonly JsonElement CanonicalContentFrameSchemaElement = JsonDocument.Parse(CanonicalContentFrameSchema).RootElement.Clone();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly OpenAiOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public OpenAiContentGenerator(OpenAiOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = NormalizeBaseAddress(options.Endpoint);
        }
    }

    public async Task<ContentGenerationResult> GenerateAsync(ContentGenerationRequest request, CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(request.ModelHint) ? _options.ContentModel : request.ModelHint.Trim();

        if (!_options.Enabled)
        {
            return ContentGenerationResult.Failure(request.PromptVersion, model, "OpenAI integration is disabled in the current environment.");
        }

        if (!_options.HasRequiredConfiguration)
        {
            return ContentGenerationResult.Failure(request.PromptVersion, model, "OpenAI integration is enabled but endpoint or API key configuration is incomplete.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        httpRequest.Headers.Add("api-key", _options.ApiKey);
        httpRequest.Content = new StringContent(
            BuildRequestPayloadJson(request, model, _options.ContentMaxOutputTokens),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return ContentGenerationResult.Failure(
                request.PromptVersion,
                model,
                $"OpenAI content generator returned {(int)response.StatusCode}: {Truncate(responseJson, 400)}");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;
            var generatedPayloadJson = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var usage = root.TryGetProperty("usage", out var usageElement)
                ? usageElement
                : default;
            var inputTokens = usage.ValueKind != JsonValueKind.Undefined &&
                              usage.TryGetProperty("prompt_tokens", out var promptTokensElement) &&
                              promptTokensElement.TryGetInt32(out var promptTokens)
                ? promptTokens
                : 0;
            var outputTokens = usage.ValueKind != JsonValueKind.Undefined &&
                               usage.TryGetProperty("completion_tokens", out var completionTokensElement) &&
                               completionTokensElement.TryGetInt32(out var completionTokens)
                ? completionTokens
                : 0;

            if (string.IsNullOrWhiteSpace(generatedPayloadJson))
            {
                return ContentGenerationResult.Failure(request.PromptVersion, model, "OpenAI content generator returned an empty content payload.");
            }

            return ContentGenerationResult.Success(
                generatedPayloadJson,
                model,
                request.PromptVersion,
                inputTokens,
                outputTokens);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            return ContentGenerationResult.Failure(
                request.PromptVersion,
                model,
                $"OpenAI content generator returned a response that could not be parsed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string BuildRequestPayloadJson(ContentGenerationRequest request, string model, int maxOutputTokens)
    {
        var payload = new
        {
            model,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = request.SystemContext
                },
                new
                {
                    role = "user",
                    content = request.UserPrompt
                }
            },
            max_completion_tokens = maxOutputTokens,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "canonical_content_frame",
                    strict = true,
                    schema = CanonicalContentFrameSchemaElement
                }
            }
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static Uri NormalizeBaseAddress(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new Uri("https://invalid.local/openai/v1/", UriKind.Absolute);
        }

        var normalized = endpoint.Trim().TrimEnd('/');
        if (!normalized.Contains("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"{normalized}/openai/v1";
        }

        return new Uri($"{normalized.TrimEnd('/')}/", UriKind.Absolute);
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
}
