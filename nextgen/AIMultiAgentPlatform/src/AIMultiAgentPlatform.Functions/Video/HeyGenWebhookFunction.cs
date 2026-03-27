using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Video;
using AIMultiAgentPlatform.Contracts.Video;
using AIMultiAgentPlatform.Functions.Http;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AIMultiAgentPlatform.Functions.Video;

public sealed class HeyGenWebhookFunction(
    ProcessHeyGenWebhookUseCase useCase,
    IVideoWebhookEndpointRegistrationRepository registrationRepository,
    HeyGenOptions heyGenOptions,
    ILogger<HeyGenWebhookFunction> logger)
{
    private const string SignatureHeaderName = "Signature";

    [Function("HeyGenWebhook")]
    public async Task<HttpResponseData> HandleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "api/integrations/heygen/webhook")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (HttpMethods.IsOptions(request.Method))
        {
            return CreateOkResponse(request);
        }

        var payloadJson = await FunctionHttp.ReadBodyAsStringAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            logger.LogWarning("Rejected empty HeyGen webhook payload.");
            return await FunctionHttp.BadRequestAsync(request, "HeyGen webhook could not be processed.", "Request body is required.", cancellationToken);
        }

        var webhookSecret = await ResolveWebhookSecretAsync(registrationRepository, heyGenOptions, cancellationToken);
        if (!string.IsNullOrWhiteSpace(webhookSecret))
        {
            if (!TryGetHeaderValue(request, SignatureHeaderName, out var providedSignature))
            {
                logger.LogWarning("Rejected HeyGen webhook because the Signature header is missing.");
                return await FunctionHttp.UnauthorizedAsync(request, "HeyGen signature validation failed.", "Missing required 'Signature' header.", cancellationToken);
            }

            if (!IsValidSignature(payloadJson, providedSignature, webhookSecret))
            {
                logger.LogWarning("Rejected HeyGen webhook because the signature did not match the configured secret.");
                return await FunctionHttp.UnauthorizedAsync(request, "HeyGen signature validation failed.", "The supplied HeyGen signature is invalid.", cancellationToken);
            }
        }

        HeyGenWebhookEnvelope? payload;
        try
        {
            payload = FunctionHttp.DeserializeJson<HeyGenWebhookEnvelope>(payloadJson);
        }
        catch (JsonException)
        {
            logger.LogWarning("Rejected HeyGen webhook because the payload body was not valid JSON.");
            return await FunctionHttp.BadRequestAsync(request, "HeyGen webhook could not be processed.", "Request body is not valid JSON.", cancellationToken);
        }

        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "HeyGen webhook could not be processed.", "Request body is required.", cancellationToken);
        }

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["HeyGenEventType"] = payload.EventType
        });

        var result = await useCase.ExecuteAsync(payload.EventType, payload.EventData, cancellationToken);
        if (result.IsFailure)
        {
            logger.LogWarning(
                "HeyGen webhook processing failed with error code {ErrorCode}.",
                result.ErrorCode ?? "unknown");

            return IsBadRequestError(result.ErrorCode)
                ? await FunctionHttp.BadRequestAsync(request, "HeyGen webhook could not be processed.", result.ErrorMessage ?? "Unknown error.", cancellationToken)
                : await InternalServerErrorAsync(request, result.ErrorMessage ?? "Unexpected webhook processing failure.", cancellationToken);
        }

        logger.LogInformation(
            "HeyGen webhook processed with outcome {Outcome} for job {VideoGenerationJobId}.",
            result.Value?.Outcome,
            result.Value?.VideoGenerationJobId);

        return CreateOkResponse(request);
    }

    private static bool TryGetHeaderValue(HttpRequestData request, string headerName, out string headerValue)
    {
        if (request.Headers.TryGetValues(headerName, out var values))
        {
            var resolvedValue = values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(resolvedValue))
            {
                headerValue = resolvedValue.Trim();
                return true;
            }
        }

        headerValue = string.Empty;
        return false;
    }

    private static bool IsValidSignature(string payloadJson, string providedSignature, string webhookSecret)
    {
        var computedSignature = ComputeSignature(payloadJson, webhookSecret);
        var providedBytes = Encoding.UTF8.GetBytes(providedSignature);
        var expectedBytes = Encoding.UTF8.GetBytes(computedSignature);

        return providedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static string ComputeSignature(string payloadJson, string webhookSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool IsBadRequestError(string? errorCode) =>
        errorCode is "heygen.webhook.event-type.required" or "heygen.webhook.video-id.required";

    private static async Task<string> ResolveWebhookSecretAsync(
        IVideoWebhookEndpointRegistrationRepository registrationRepository,
        HeyGenOptions heyGenOptions,
        CancellationToken cancellationToken)
    {
        var registration = await registrationRepository.FindByProviderAsync("HeyGen", cancellationToken);
        if (registration is not null && !string.IsNullOrWhiteSpace(registration.Secret))
        {
            return registration.Secret;
        }

        return heyGenOptions.WebhookSecret;
    }

    private static HttpResponseData CreateOkResponse(HttpRequestData request)
    {
        var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        return response;
    }

    private static async Task<HttpResponseData> InternalServerErrorAsync(
        HttpRequestData request,
        string detail,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync($$"""{"title":"HeyGen webhook could not be finalized.","detail":"{{EscapeJson(detail)}}"}""", cancellationToken);
        return response;
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed record HeyGenWebhookEnvelope(
        [property: JsonPropertyName("event_type")] string EventType,
        [property: JsonPropertyName("event_data")] JsonElement EventData);
}

internal static class HttpMethods
{
    public static bool IsOptions(string? method) =>
        string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase);
}
