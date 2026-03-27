namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record ProductionInfrastructureOptions(
    bool SqlSkeletonEnabled,
    bool ServiceBusSkeletonEnabled,
    bool CommandOutboxEnabled)
{
    public static ProductionInfrastructureOptions Disabled => new(false, false, false);
}
