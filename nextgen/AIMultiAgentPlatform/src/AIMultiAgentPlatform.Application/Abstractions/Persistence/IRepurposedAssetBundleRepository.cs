using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IRepurposedAssetBundleRepository
{
    Task SaveAsync(RepurposedAssetBundle bundle, CancellationToken cancellationToken);

    Task<RepurposedAssetBundle?> FindByIdAsync(string bundleId, CancellationToken cancellationToken);

    Task<RepurposedAssetBundle?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken);
}
