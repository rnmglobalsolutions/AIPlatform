using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Leads;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryLeadProfileRepository : ILeadProfileRepository
{
    private readonly ConcurrentDictionary<string, LeadProfile> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(LeadProfile leadProfile, CancellationToken cancellationToken)
    {
        _items[BuildKey(leadProfile.TenantId.Value, leadProfile.ManyChatContactId)] = leadProfile;
        return Task.CompletedTask;
    }

    public Task<LeadProfile?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(tenantId, manyChatContactId));

    public LeadProfile? Find(string tenantId, string manyChatContactId) =>
        _items.TryGetValue(BuildKey(tenantId, manyChatContactId), out var leadProfile) ? leadProfile : null;

    public IReadOnlyList<LeadProfile> ListAll() => _items.Values.ToArray();

    private static string BuildKey(string tenantId, string manyChatContactId) => $"{tenantId}::{manyChatContactId}";
}
