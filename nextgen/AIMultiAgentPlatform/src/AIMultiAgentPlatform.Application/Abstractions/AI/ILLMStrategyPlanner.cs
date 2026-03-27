namespace AIMultiAgentPlatform.Application.Abstractions.AI;

public interface ILLMStrategyPlanner
{
    Task<StrategyPlannerResult> GenerateAsync(StrategyPlannerRequest request, CancellationToken cancellationToken);
}
