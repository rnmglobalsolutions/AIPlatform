using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Leads;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryManyChatContactStateRepository : IManyChatContactStateRepository
{
    private readonly ConcurrentDictionary<string, ManyChatContactState> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(ManyChatContactState contactState, CancellationToken cancellationToken)
    {
        _items[BuildKey(contactState.TenantId.Value, contactState.ManyChatContactId)] = contactState;
        return Task.CompletedTask;
    }

    public Task<ManyChatContactState?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(tenantId, manyChatContactId));

    public ManyChatContactState? Find(string tenantId, string manyChatContactId) =>
        _items.TryGetValue(BuildKey(tenantId, manyChatContactId), out var contactState) ? contactState : null;

    private static string BuildKey(string tenantId, string manyChatContactId) => $"{tenantId}::{manyChatContactId}";
}
