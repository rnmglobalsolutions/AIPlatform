using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.AI.InMemory;

public sealed class InMemoryLlmStrategyPlanner : ILLMStrategyPlanner
{
    private readonly OpenAiOptions _options;
    private readonly FeatureFlagOptions _featureFlags;

    public InMemoryLlmStrategyPlanner(OpenAiOptions options, FeatureFlagOptions featureFlags)
    {
        _options = options;
        _featureFlags = featureFlags;
    }

    public Task<StrategyPlannerResult> GenerateAsync(StrategyPlannerRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(
            StrategyPlannerResult.Failure(
                request.PromptVersion,
                string.IsNullOrWhiteSpace(request.ModelHint) ? _options.StrategyModel : request.ModelHint,
                ResolveFailureReason()));

    private string ResolveFailureReason()
    {
        if (!_featureFlags.EnableLlmStrategyPlanning)
        {
            return "LLM strategy planning feature flag is disabled.";
        }

        if (!_options.Enabled)
        {
            return "OpenAI integration is disabled in the current environment.";
        }

        if (!_options.HasRequiredConfiguration)
        {
            return "OpenAI integration is enabled but endpoint or API key configuration is incomplete.";
        }

        return "LLM strategy planning is enabled, but no concrete strategy planner has been registered for the current environment.";
    }
}
