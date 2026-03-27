using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IPublishedContentRecordRepository
{
    Task SaveAsync(PublishedContentRecord record, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublishedContentRecord>> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken);
}
