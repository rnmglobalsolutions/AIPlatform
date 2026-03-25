using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Domain.Editorial;
using Azure.Data.Tables;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.TableStorage;

internal sealed class TableStorageEditorialBacklogRepository : TableStorageJsonRepositoryBase, IEditorialBacklogRepository
{
    public TableStorageEditorialBacklogRepository(TableServiceClient tableServiceClient, TableStorageOptions options)
        : base(tableServiceClient.GetTableClient(options.EditorialBacklogTableName))
    {
    }

    protected override string PartitionKey => "EditorialBacklog";

    public Task SaveAsync(EditorialBacklog backlog, CancellationToken cancellationToken) =>
        SaveDocumentAsync(backlog.EditorialBacklogId, backlog, cancellationToken);

    public Task<EditorialBacklog?> FindByIdAsync(string backlogId, CancellationToken cancellationToken) =>
        FindDocumentAsync<EditorialBacklog>(backlogId, cancellationToken);
}
