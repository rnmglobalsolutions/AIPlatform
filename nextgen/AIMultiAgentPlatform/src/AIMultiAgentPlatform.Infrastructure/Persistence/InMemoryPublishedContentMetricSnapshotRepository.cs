using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryPublishedContentMetricSnapshotRepository : IPublishedContentMetricSnapshotRepository
{
    private readonly ConcurrentDictionary<string, PublishedContentMetricSnapshot> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(PublishedContentMetricSnapshot snapshot, CancellationToken cancellationToken)
    {
        _items[snapshot.PublishedContentMetricSnapshotId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PublishedContentMetricSnapshot>> FindByPublishedContentRecordIdAsync(string publishedContentRecordId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PublishedContentMetricSnapshot>>(
            _items.Values
                .Where(item => item.PublishedContentRecordId == publishedContentRecordId)
                .OrderBy(item => item.CapturedUtc)
                .ToArray());

    public IReadOnlyList<PublishedContentMetricSnapshot> ListAll() => _items.Values.ToArray();
}
