using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.AI.InMemory;

public sealed class InMemoryLlmContentGenerator : ILLMContentGenerator
{
    private readonly OpenAiOptions _options;
    private readonly FeatureFlagOptions _featureFlags;

    public InMemoryLlmContentGenerator(OpenAiOptions options, FeatureFlagOptions featureFlags)
    {
        _options = options;
        _featureFlags = featureFlags;
    }

    public Task<ContentGenerationResult> GenerateAsync(ContentGenerationRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(
            ContentGenerationResult.Failure(
                request.PromptVersion,
                string.IsNullOrWhiteSpace(request.ModelHint) ? _options.ContentModel : request.ModelHint,
                ResolveFailureReason()));

    private string ResolveFailureReason()
    {
        if (!_featureFlags.EnableLlmContentGeneration)
        {
            return "LLM content generation feature flag is disabled.";
        }

        if (!_options.Enabled)
        {
            return "OpenAI integration is disabled in the current environment.";
        }

        if (!_options.HasRequiredConfiguration)
        {
            return "OpenAI integration is enabled but endpoint or API key configuration is incomplete.";
        }

        return "LLM content generation is enabled, but no concrete content generator has been registered for the current environment.";
    }
}
