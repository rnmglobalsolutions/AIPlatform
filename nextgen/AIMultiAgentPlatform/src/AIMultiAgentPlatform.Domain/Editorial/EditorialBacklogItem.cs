namespace AIMultiAgentPlatform.Domain.Editorial;

public sealed record EditorialBacklogItem(
    int Sequence,
    int PlannedOffsetDays,
    ContentCategory Category,
    PrimaryFormat PrimaryFormat,
    string Topic,
    string Angle,
    string HookDirection,
    string LeadGoal,
    bool UsesCallToActionKeyword);
