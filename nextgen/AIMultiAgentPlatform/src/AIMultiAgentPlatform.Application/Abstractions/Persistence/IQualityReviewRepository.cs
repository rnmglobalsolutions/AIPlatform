using AIMultiAgentPlatform.Domain.Reviewing;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IQualityReviewRepository
{
    Task SaveAsync(QualityReview review, CancellationToken cancellationToken);

    Task<QualityReview?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken);
}
