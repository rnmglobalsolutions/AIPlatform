using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Reviewing;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryComplianceReviewRepository : IComplianceReviewRepository
{
    private readonly ConcurrentDictionary<string, ComplianceReview> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(ComplianceReview review, CancellationToken cancellationToken)
    {
        _items[review.ComplianceReviewId] = review;
        return Task.CompletedTask;
    }

    public Task<ComplianceReview?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == requestId));

    public ComplianceReview? Find(string reviewId) => _items.TryGetValue(reviewId, out var review) ? review : null;
}
