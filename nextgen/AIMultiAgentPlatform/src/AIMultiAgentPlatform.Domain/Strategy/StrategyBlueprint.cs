namespace AIMultiAgentPlatform.Domain.Strategy;

public sealed record StrategyBlueprint(
    string StrategicNarrative,
    IReadOnlyList<string> ContentPillars,
    int DailyPostingCadenceDays,
    int VideoCadenceDays,
    IReadOnlyList<BacklogBlueprintItem> BacklogBlueprintItems,
    ContentPlanTier ContentPlanTier = ContentPlanTier.Starter,
    int MonthlyVideoTarget = 8);
