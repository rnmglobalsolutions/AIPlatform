using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryCaptionAssetRepository : ICaptionAssetRepository
{
    private readonly ConcurrentDictionary<string, CaptionAsset> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(CaptionAsset asset, CancellationToken cancellationToken)
    {
        _items[asset.CaptionAssetId] = asset;
        return Task.CompletedTask;
    }

    public Task<CaptionAsset?> FindByIdAsync(string captionAssetId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(captionAssetId));

    public Task<CaptionAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == requestId));

    public CaptionAsset? Find(string captionAssetId) => _items.TryGetValue(captionAssetId, out var asset) ? asset : null;
}
