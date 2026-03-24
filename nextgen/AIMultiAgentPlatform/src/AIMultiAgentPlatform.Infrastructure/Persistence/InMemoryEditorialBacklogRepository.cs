using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Editorial;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryEditorialBacklogRepository : IEditorialBacklogRepository
{
    private readonly ConcurrentDictionary<string, EditorialBacklog> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(EditorialBacklog backlog, CancellationToken cancellationToken)
    {
        _items[backlog.EditorialBacklogId] = backlog;
        return Task.CompletedTask;
    }

    public Task<EditorialBacklog?> FindByIdAsync(string backlogId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(backlogId));

    public EditorialBacklog? Find(string backlogId) => _items.TryGetValue(backlogId, out var backlog) ? backlog : null;
}
