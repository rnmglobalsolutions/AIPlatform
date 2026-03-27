namespace AIMultiAgentPlatform.Application.Abstractions.Video;

public interface IVideoAssetStore
{
    Task<VideoAssetStorageResult> StoreAsync(VideoAssetStorageRequest request, CancellationToken cancellationToken);
}
