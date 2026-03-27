namespace AIMultiAgentPlatform.Application.Abstractions.AI;

public interface ILLMContentGenerator
{
    Task<ContentGenerationResult> GenerateAsync(ContentGenerationRequest request, CancellationToken cancellationToken);
}
