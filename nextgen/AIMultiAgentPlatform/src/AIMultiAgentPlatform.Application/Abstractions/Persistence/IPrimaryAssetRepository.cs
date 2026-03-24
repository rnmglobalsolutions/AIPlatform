using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IPrimaryAssetRepository
{
    Task SaveAsync(PrimaryAsset asset, CancellationToken cancellationToken);

    Task<PrimaryAsset?> FindByIdAsync(string primaryAssetId, CancellationToken cancellationToken);

    Task<PrimaryAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken);
}
