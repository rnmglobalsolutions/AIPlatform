using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Domain.Tenants;
using Azure.Data.Tables;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.TableStorage;

internal sealed class TableStorageTenantRepository : TableStorageJsonRepositoryBase, ITenantRepository
{
    public TableStorageTenantRepository(TableServiceClient tableServiceClient, TableStorageOptions options)
        : base(tableServiceClient.GetTableClient(options.TenantTableName))
    {
    }

    protected override string PartitionKey => "Tenant";

    public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) =>
        SaveDocumentAsync(tenant.TenantId.Value, tenant, cancellationToken);

    public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
        FindDocumentAsync<Tenant>(tenantId, cancellationToken);
}
