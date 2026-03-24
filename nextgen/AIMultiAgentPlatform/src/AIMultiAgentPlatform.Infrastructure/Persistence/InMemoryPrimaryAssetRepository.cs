using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryPrimaryAssetRepository : IPrimaryAssetRepository
{
    private readonly ConcurrentDictionary<string, PrimaryAsset> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(PrimaryAsset asset, CancellationToken cancellationToken)
    {
        _items[asset.PrimaryAssetId] = asset;
        return Task.CompletedTask;
    }

    public Task<PrimaryAsset?> FindByIdAsync(string primaryAssetId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(primaryAssetId));

    public Task<PrimaryAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == requestId));

    public PrimaryAsset? Find(string primaryAssetId) => _items.TryGetValue(primaryAssetId, out var asset) ? asset : null;

    public IReadOnlyList<PrimaryAsset> ListAll() => _items.Values.ToArray();
}
