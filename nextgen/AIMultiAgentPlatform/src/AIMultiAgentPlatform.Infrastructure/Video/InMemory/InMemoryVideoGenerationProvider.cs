using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Video.InMemory;

public sealed class InMemoryVideoGenerationProvider : IVideoGenerationProvider
{
    private readonly FeatureFlagOptions _featureFlags;

    public InMemoryVideoGenerationProvider(FeatureFlagOptions featureFlags) => _featureFlags = featureFlags;

    public string ProviderName => "InMemoryVideoProvider";

    public Task<VideoGenerationSubmissionResult> SubmitAsync(VideoGenerationRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(
            VideoGenerationSubmissionResult.Rejected(
                ProviderName,
                ResolveFailureReason()));

    public Task<VideoGenerationStatusResult> GetStatusAsync(string providerJobId, CancellationToken cancellationToken) =>
        Task.FromResult(
            new VideoGenerationStatusResult(
                providerJobId,
                ProviderName,
                "Unavailable",
                string.Empty,
                string.Empty,
                ResolveFailureReason()));

    private string ResolveFailureReason() =>
        !_featureFlags.EnableVideoGeneration
            ? "Video generation feature flag is disabled."
            : "Video generation is enabled, but no concrete provider has been registered for the current environment.";
}
