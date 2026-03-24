using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryDailyContentRequestRepository : IDailyContentRequestRepository
{
    private readonly ConcurrentDictionary<string, DailyContentRequest> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(DailyContentRequest request, CancellationToken cancellationToken)
    {
        _items[request.DailyContentRequestId] = request;
        return Task.CompletedTask;
    }

    public Task<DailyContentRequest?> FindByIdAsync(string requestId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(requestId));

    public DailyContentRequest? Find(string requestId) => _items.TryGetValue(requestId, out var request) ? request : null;

    public IReadOnlyList<DailyContentRequest> ListAll() => _items.Values.ToArray();
}
