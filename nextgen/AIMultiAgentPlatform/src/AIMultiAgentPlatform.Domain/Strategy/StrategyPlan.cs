using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Strategy;

public sealed record StrategyPlan(
    string StrategyPlanId,
    TenantId TenantId,
    string StrategicNarrative,
    IReadOnlyList<string> ContentPillars,
    int DailyPostingCadenceDays,
    int VideoCadenceDays,
    DateTime CreatedUtc);
