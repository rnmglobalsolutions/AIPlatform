using AIMultiAgentPlatform.Domain.Strategy;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IStrategyPlanRepository
{
    Task SaveAsync(StrategyPlan strategyPlan, CancellationToken cancellationToken);
}
