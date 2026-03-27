using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryPublishedContentRecordRepository : IPublishedContentRecordRepository
{
    private readonly ConcurrentDictionary<string, PublishedContentRecord> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(PublishedContentRecord record, CancellationToken cancellationToken)
    {
        _items[record.PublishedContentRecordId] = record;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PublishedContentRecord>> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PublishedContentRecord>>(_items.Values.Where(item => item.DailyContentRequestId == dailyContentRequestId).ToArray());

    public IReadOnlyList<PublishedContentRecord> ListAll() => _items.Values.ToArray();
}
