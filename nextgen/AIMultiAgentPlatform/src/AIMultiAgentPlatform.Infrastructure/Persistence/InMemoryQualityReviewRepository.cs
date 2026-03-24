using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Reviewing;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryQualityReviewRepository : IQualityReviewRepository
{
    private readonly ConcurrentDictionary<string, QualityReview> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(QualityReview review, CancellationToken cancellationToken)
    {
        _items[review.QualityReviewId] = review;
        return Task.CompletedTask;
    }

    public Task<QualityReview?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == requestId));

    public QualityReview? Find(string reviewId) => _items.TryGetValue(reviewId, out var review) ? review : null;

    public IReadOnlyList<QualityReview> ListAll() => _items.Values.ToArray();
}
