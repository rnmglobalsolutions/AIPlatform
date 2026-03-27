using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Publishing.InMemory;

public sealed class InMemoryPublishingProvider : IPublishingProvider
{
    private readonly FeatureFlagOptions _featureFlags;

    public InMemoryPublishingProvider(FeatureFlagOptions featureFlags) => _featureFlags = featureFlags;

    public string ProviderName => "InMemoryPublishingProvider";

    public Task<PublishingResult> PublishAsync(PublishingRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(
            PublishingResult.Failure(
                request.Platform,
                ResolveFailureReason()));

    private string ResolveFailureReason() =>
        !_featureFlags.EnableSocialPublishing
            ? "Social publishing feature flag is disabled."
            : "Social publishing is enabled, but no concrete provider has been registered for the current environment.";
}
