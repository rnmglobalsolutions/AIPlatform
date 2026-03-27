namespace AIMultiAgentPlatform.Application.Abstractions.AI;

public interface IContentGuardrailValidator
{
    Task<GuardrailValidationResult> ValidateAsync(ContentGuardrailValidationRequest request, CancellationToken cancellationToken);
}
