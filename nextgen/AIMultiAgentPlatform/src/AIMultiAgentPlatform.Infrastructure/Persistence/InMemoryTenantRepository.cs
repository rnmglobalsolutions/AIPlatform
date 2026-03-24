using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryTenantRepository : ITenantRepository
{
    private readonly ConcurrentDictionary<string, Tenant> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        _items[tenant.TenantId.Value] = tenant;
        return Task.CompletedTask;
    }

    public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(tenantId));

    public Tenant? Find(string tenantId) => _items.TryGetValue(tenantId, out var tenant) ? tenant : null;
}
