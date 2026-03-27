using AIMultiAgentPlatform.Domain.Editorial;

namespace AIMultiAgentPlatform.Domain.Strategy;

public sealed record BacklogBlueprintItem(
    int Sequence,
    int PlannedOffsetDays,
    ContentCategory Category,
    PrimaryFormat PrimaryFormat,
    string Topic,
    string Angle,
    string HookDirection,
    string LeadGoal,
    bool UsesCallToActionKeyword);
