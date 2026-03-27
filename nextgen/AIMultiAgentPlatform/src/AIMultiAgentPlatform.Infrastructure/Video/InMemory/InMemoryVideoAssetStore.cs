using AIMultiAgentPlatform.Application.Abstractions.Video;

namespace AIMultiAgentPlatform.Infrastructure.Video.InMemory;

public sealed class InMemoryVideoAssetStore : IVideoAssetStore
{
    public Task<VideoAssetStorageResult> StoreAsync(VideoAssetStorageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderVideoUrl))
        {
            return Task.FromResult(
                VideoAssetStorageResult.Failure(
                    "InMemoryVideoAssetStore",
                    "Provider video URL is required."));
        }

        return Task.FromResult(
            VideoAssetStorageResult.Success(
                request.ProviderVideoUrl,
                "InMemoryVideoAssetStore"));
    }
}
