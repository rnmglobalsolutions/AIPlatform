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

    public Task<PublishedContentRecord?> FindByIdAsync(string publishedContentRecordId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(publishedContentRecordId, out var record) ? record : null);

    public Task<IReadOnlyList<PublishedContentRecord>> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PublishedContentRecord>>(_items.Values.Where(item => item.DailyContentRequestId == dailyContentRequestId).ToArray());

    public Task<IReadOnlyList<PublishedContentRecord>> FindBySchedulingJobIdAsync(string schedulingJobId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PublishedContentRecord>>(_items.Values.Where(item => item.SchedulingJobId == schedulingJobId).ToArray());

    public IReadOnlyList<PublishedContentRecord> ListAll() => _items.Values.ToArray();
}
