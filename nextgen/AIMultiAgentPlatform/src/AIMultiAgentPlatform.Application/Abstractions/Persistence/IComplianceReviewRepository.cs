using AIMultiAgentPlatform.Domain.Reviewing;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IComplianceReviewRepository
{
    Task SaveAsync(ComplianceReview review, CancellationToken cancellationToken);

    Task<ComplianceReview?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken);
}
