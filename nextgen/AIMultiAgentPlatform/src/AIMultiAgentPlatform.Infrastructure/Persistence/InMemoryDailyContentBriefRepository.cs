using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryDailyContentBriefRepository : IDailyContentBriefRepository
{
    private readonly ConcurrentDictionary<string, DailyContentBrief> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(DailyContentBrief brief, CancellationToken cancellationToken)
    {
        _items[brief.DailyContentBriefId] = brief;
        return Task.CompletedTask;
    }

    public Task<DailyContentBrief?> FindByIdAsync(string briefId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(briefId));

    public Task<DailyContentBrief?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == requestId));

    public DailyContentBrief? Find(string briefId) => _items.TryGetValue(briefId, out var brief) ? brief : null;
}
