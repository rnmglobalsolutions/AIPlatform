using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface ICaptionAssetRepository
{
    Task SaveAsync(CaptionAsset asset, CancellationToken cancellationToken);

    Task<CaptionAsset?> FindByIdAsync(string captionAssetId, CancellationToken cancellationToken);

    Task<CaptionAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken);
}
