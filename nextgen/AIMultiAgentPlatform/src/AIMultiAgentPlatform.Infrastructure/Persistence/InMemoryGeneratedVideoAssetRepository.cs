using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryGeneratedVideoAssetRepository : IGeneratedVideoAssetRepository
{
    private readonly ConcurrentDictionary<string, GeneratedVideoAsset> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(GeneratedVideoAsset asset, CancellationToken cancellationToken)
    {
        _items[asset.GeneratedVideoAssetId] = asset;
        return Task.CompletedTask;
    }

    public Task<GeneratedVideoAsset?> FindByJobIdAsync(string videoGenerationJobId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.VideoGenerationJobId == videoGenerationJobId));

    public Task<GeneratedVideoAsset?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == dailyContentRequestId));

    public GeneratedVideoAsset? Find(string generatedVideoAssetId) =>
        _items.TryGetValue(generatedVideoAssetId, out var asset) ? asset : null;

    public IReadOnlyList<GeneratedVideoAsset> ListAll() => _items.Values.ToArray();
}
