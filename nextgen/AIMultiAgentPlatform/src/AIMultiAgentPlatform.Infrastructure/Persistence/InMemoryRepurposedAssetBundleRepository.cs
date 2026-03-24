using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryRepurposedAssetBundleRepository : IRepurposedAssetBundleRepository
{
    private readonly ConcurrentDictionary<string, RepurposedAssetBundle> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(RepurposedAssetBundle bundle, CancellationToken cancellationToken)
    {
        _items[bundle.RepurposedAssetBundleId] = bundle;
        return Task.CompletedTask;
    }

    public Task<RepurposedAssetBundle?> FindByIdAsync(string bundleId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(bundleId));

    public Task<RepurposedAssetBundle?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == requestId));

    public RepurposedAssetBundle? Find(string bundleId) => _items.TryGetValue(bundleId, out var bundle) ? bundle : null;
}
