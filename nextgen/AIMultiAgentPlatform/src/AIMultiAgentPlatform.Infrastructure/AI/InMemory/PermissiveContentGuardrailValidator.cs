using AIMultiAgentPlatform.Application.Abstractions.AI;
using System.Text.Json;

namespace AIMultiAgentPlatform.Infrastructure.AI.InMemory;

public sealed class PermissiveContentGuardrailValidator : IContentGuardrailValidator
{
    public Task<GuardrailValidationResult> ValidateAsync(ContentGuardrailValidationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Workflow))
        {
            return Task.FromResult(
                GuardrailValidationResult.Invalid(["Workflow is required for guardrail validation."]));
        }

        if (string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            return Task.FromResult(
                GuardrailValidationResult.Invalid(["PayloadJson is required for guardrail validation."]));
        }

        try
        {
            using var _ = JsonDocument.Parse(request.PayloadJson);
        }
        catch (JsonException exception)
        {
            return Task.FromResult(
                GuardrailValidationResult.Invalid([$"PayloadJson must be valid JSON. {exception.Message}"]));
        }

        var warnings = new List<string>();
        if (request.BlockedTopics is not null)
        {
            foreach (var blockedTopic in request.BlockedTopics)
            {
                if (string.IsNullOrWhiteSpace(blockedTopic))
                {
                    continue;
                }

                if (request.PayloadJson.Contains(blockedTopic, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Payload references blocked topic '{blockedTopic}'.");
                }
            }
        }

        return Task.FromResult(GuardrailValidationResult.Valid(warnings));
    }
}
