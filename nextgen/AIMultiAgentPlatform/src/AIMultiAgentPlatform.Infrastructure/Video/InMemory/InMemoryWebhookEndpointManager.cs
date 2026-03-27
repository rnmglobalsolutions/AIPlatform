using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Video.InMemory;

public sealed class InMemoryWebhookEndpointManager : IWebhookEndpointManager
{
    private readonly FeatureFlagOptions _featureFlags;

    public InMemoryWebhookEndpointManager(FeatureFlagOptions featureFlags) => _featureFlags = featureFlags;

    public Task<WebhookEndpointListResult> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(WebhookEndpointListResult.Failure(ResolveFailureReason()));

    public Task<WebhookEndpointMutationResult> CreateAsync(string url, IReadOnlyList<string> events, CancellationToken cancellationToken) =>
        Task.FromResult(WebhookEndpointMutationResult.Failure(ResolveFailureReason()));

    public Task<WebhookEndpointMutationResult> UpdateAsync(string endpointId, string url, IReadOnlyList<string> events, CancellationToken cancellationToken) =>
        Task.FromResult(WebhookEndpointMutationResult.Failure(ResolveFailureReason()));

    public Task<WebhookEndpointDeletionResult> DeleteAsync(string endpointId, CancellationToken cancellationToken) =>
        Task.FromResult(WebhookEndpointDeletionResult.Failure(ResolveFailureReason()));

    private string ResolveFailureReason() =>
        !_featureFlags.EnableVideoGeneration
            ? "Video generation feature flag is disabled."
            : "Video generation is enabled, but no concrete HeyGen webhook manager has been registered for the current environment.";
}
