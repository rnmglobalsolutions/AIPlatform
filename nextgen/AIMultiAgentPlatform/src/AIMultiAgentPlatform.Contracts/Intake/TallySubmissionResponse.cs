namespace AIMultiAgentPlatform.Contracts.Intake;

public sealed record TallySubmissionResponse(
    string TenantId,
    string Slug,
    string StrategyPlanId,
    string EditorialBacklogId,
    int BacklogItemCount);
