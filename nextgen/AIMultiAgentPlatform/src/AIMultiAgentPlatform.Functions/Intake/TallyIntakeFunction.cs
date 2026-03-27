using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIMultiAgentPlatform.Functions.Intake;

public sealed class TallyIntakeFunction(
    ProcessTallySubmissionUseCase useCase,
    EnqueueProcessTallySubmissionUseCase enqueueUseCase,
    IConfiguration configuration,
    ILogger<TallyIntakeFunction> logger)
{
    private const string TallySignatureHeaderName = "Tally-Signature";

    [Function("TallyIntakePost")]
    public async Task<HttpResponseData> PostAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "api/intake/tally")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payloadJson = await FunctionHttp.ReadBodyAsStringAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            logger.LogWarning("Rejected empty Tally webhook payload.");
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be processed.", "Request body is required.", cancellationToken);
        }

        var signingSecret = ResolveSigningSecret(configuration);
        if (!string.IsNullOrWhiteSpace(signingSecret))
        {
            if (!TryGetHeaderValue(request, TallySignatureHeaderName, out var providedSignature))
            {
                logger.LogWarning("Rejected Tally webhook because the {HeaderName} header was missing while a signing secret is configured.", TallySignatureHeaderName);
                return await FunctionHttp.UnauthorizedAsync(request, "Tally signature validation failed.", $"Missing required '{TallySignatureHeaderName}' header.", cancellationToken);
            }

            if (!IsValidSignature(payloadJson, providedSignature, signingSecret))
            {
                logger.LogWarning("Rejected Tally webhook because the {HeaderName} header did not match the configured signing secret.", TallySignatureHeaderName);
                return await FunctionHttp.UnauthorizedAsync(request, "Tally signature validation failed.", "The supplied Tally signature is invalid.", cancellationToken);
            }
        }

        TallySubmissionRequest? payload;
        try
        {
            payload = FunctionHttp.DeserializeJson<TallySubmissionRequest>(payloadJson);
        }
        catch (JsonException)
        {
            logger.LogWarning("Rejected Tally webhook because the payload body was not valid JSON.");
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be processed.", "Request body is not valid JSON.", cancellationToken);
        }

        if (payload is null)
        {
            logger.LogWarning("Rejected Tally webhook because deserialization returned no payload.");
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be processed.", "Request body is required.", cancellationToken);
        }

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ExternalSubmissionId"] = payload.ExternalSubmissionId
        });

        logger.LogInformation("Accepted Tally webhook for intake processing.");

        Result<TallySubmissionResponse> result;
        try
        {
            result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(payload), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tally intake processing failed unexpectedly.");
            throw;
        }

        if (!result.IsSuccess || result.Value is null)
        {
            logger.LogWarning(
                "Tally intake processing returned a business failure with error code {ErrorCode}.",
                result.ErrorCode ?? "unknown");
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be processed.", Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."), cancellationToken);
        }

        logger.LogInformation(
            "Tally intake processing completed successfully for tenant {TenantId} with backlog size {BacklogItemCount}.",
            result.Value.TenantId,
            result.Value.BacklogItemCount);

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }

    [Function("TallyIntakeEnqueue")]
    public async Task<HttpResponseData> EnqueueAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "api/intake/tally/enqueue")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payloadJson = await FunctionHttp.ReadBodyAsStringAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            logger.LogWarning("Rejected empty Tally enqueue payload.");
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be enqueued.", "Request body is required.", cancellationToken);
        }

        var signingSecret = ResolveSigningSecret(configuration);
        if (!string.IsNullOrWhiteSpace(signingSecret))
        {
            if (!TryGetHeaderValue(request, TallySignatureHeaderName, out var providedSignature))
            {
                logger.LogWarning("Rejected Tally enqueue because the {HeaderName} header was missing while a signing secret is configured.", TallySignatureHeaderName);
                return await FunctionHttp.UnauthorizedAsync(request, "Tally signature validation failed.", $"Missing required '{TallySignatureHeaderName}' header.", cancellationToken);
            }

            if (!IsValidSignature(payloadJson, providedSignature, signingSecret))
            {
                logger.LogWarning("Rejected Tally enqueue because the {HeaderName} header did not match the configured signing secret.", TallySignatureHeaderName);
                return await FunctionHttp.UnauthorizedAsync(request, "Tally signature validation failed.", "The supplied Tally signature is invalid.", cancellationToken);
            }
        }

        TallySubmissionRequest? payload;
        try
        {
            payload = FunctionHttp.DeserializeJson<TallySubmissionRequest>(payloadJson);
        }
        catch (JsonException)
        {
            logger.LogWarning("Rejected Tally enqueue because the payload body was not valid JSON.");
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be enqueued.", "Request body is not valid JSON.", cancellationToken);
        }

        if (payload is null)
        {
            logger.LogWarning("Rejected Tally enqueue because deserialization returned no payload.");
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be enqueued.", "Request body is required.", cancellationToken);
        }

        var result = await enqueueUseCase.ExecuteAsync(new ProcessTallySubmissionCommand(payload), cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            logger.LogWarning(
                "Tally enqueue returned a business failure with error code {ErrorCode}.",
                result.ErrorCode ?? "unknown");
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be enqueued.", Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."), cancellationToken);
        }

        logger.LogInformation(
            "Tally submission {ExternalSubmissionId} was enqueued with message {MessageId}.",
            payload.ExternalSubmissionId,
            result.Value.MessageId);

        return await FunctionHttp.AcceptedAsync(request, result.Value, cancellationToken);
    }

    private static string? ResolveSigningSecret(IConfiguration configuration)
    {
        var configuredSecret = configuration["TallyWebhook:SigningSecret"];
        return string.IsNullOrWhiteSpace(configuredSecret) ? null : configuredSecret.Trim();
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

    private static bool IsValidSignature(string payloadJson, string providedSignature, string signingSecret)
    {
        var expectedSignature = ComputeSignature(payloadJson, signingSecret);
        var providedBytes = Encoding.UTF8.GetBytes(providedSignature);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);

        return providedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static string ComputeSignature(string payloadJson, string signingSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
        return Convert.ToBase64String(hashBytes);
    }
}
