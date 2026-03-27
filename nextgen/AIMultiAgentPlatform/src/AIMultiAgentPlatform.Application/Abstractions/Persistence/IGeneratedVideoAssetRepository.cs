using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IGeneratedVideoAssetRepository
{
    Task SaveAsync(GeneratedVideoAsset asset, CancellationToken cancellationToken);

    Task<GeneratedVideoAsset?> FindByJobIdAsync(string videoGenerationJobId, CancellationToken cancellationToken);

    Task<GeneratedVideoAsset?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken);
}
