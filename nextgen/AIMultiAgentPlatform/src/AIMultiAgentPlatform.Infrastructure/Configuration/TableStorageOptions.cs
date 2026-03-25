using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record TableStorageOptions(
    string ConnectionString,
    string TenantTableName,
    string StrategyPlanTableName,
    string EditorialBacklogTableName)
{
    public static TableStorageOptions Resolve(IConfiguration configuration)
    {
        var connectionString =
            configuration["Storage:TableConnectionString"] ??
            configuration["Storage__TableConnectionString"] ??
            configuration["AzureWebJobsStorage"] ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Table storage mode requires Storage:TableConnectionString or AzureWebJobsStorage.");
        }

        return new TableStorageOptions(
            connectionString,
            configuration["Storage:TenantTableName"] ?? "AimapTenants",
            configuration["Storage:StrategyPlanTableName"] ?? "AimapStrategyPlans",
            configuration["Storage:EditorialBacklogTableName"] ?? "AimapEditorialBacklogs");
    }
}
