using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface ITenantRepository
{
    Task SaveAsync(Tenant tenant, CancellationToken cancellationToken);

    Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken);
}
