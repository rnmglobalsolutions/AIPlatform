using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IPublishedContentMetricSnapshotRepository
{
    Task SaveAsync(PublishedContentMetricSnapshot snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublishedContentMetricSnapshot>> FindByPublishedContentRecordIdAsync(string publishedContentRecordId, CancellationToken cancellationToken);
}
