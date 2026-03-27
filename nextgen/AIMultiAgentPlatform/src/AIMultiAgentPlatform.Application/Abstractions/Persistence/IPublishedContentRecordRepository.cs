using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IPublishedContentRecordRepository
{
    Task SaveAsync(PublishedContentRecord record, CancellationToken cancellationToken);

    Task<PublishedContentRecord?> FindByIdAsync(string publishedContentRecordId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublishedContentRecord>> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublishedContentRecord>> FindBySchedulingJobIdAsync(string schedulingJobId, CancellationToken cancellationToken);
}
