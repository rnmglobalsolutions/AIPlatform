using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Strategy;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryStrategyPlanRepository : IStrategyPlanRepository
{
    private readonly ConcurrentDictionary<string, StrategyPlan> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(StrategyPlan strategyPlan, CancellationToken cancellationToken)
    {
        _items[strategyPlan.StrategyPlanId] = strategyPlan;
        return Task.CompletedTask;
    }

    public StrategyPlan? Find(string strategyPlanId) => _items.TryGetValue(strategyPlanId, out var strategyPlan) ? strategyPlan : null;
}
