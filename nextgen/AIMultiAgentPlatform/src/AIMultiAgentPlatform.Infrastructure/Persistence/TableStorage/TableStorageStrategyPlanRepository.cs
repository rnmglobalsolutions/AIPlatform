using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Domain.Strategy;
using Azure.Data.Tables;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.TableStorage;

internal sealed class TableStorageStrategyPlanRepository : TableStorageJsonRepositoryBase, IStrategyPlanRepository
{
    public TableStorageStrategyPlanRepository(TableServiceClient tableServiceClient, TableStorageOptions options)
        : base(tableServiceClient.GetTableClient(options.StrategyPlanTableName))
    {
    }

    protected override string PartitionKey => "StrategyPlan";

    public Task SaveAsync(StrategyPlan strategyPlan, CancellationToken cancellationToken) =>
        SaveDocumentAsync(strategyPlan.StrategyPlanId, strategyPlan, cancellationToken);
}
