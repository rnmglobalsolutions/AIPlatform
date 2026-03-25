namespace AIMultiAgentPlatform.Domain.Intake;

public sealed record TallySubmissionReceipt(
    string ExternalSubmissionId,
    string TenantId,
    string Slug,
    string StrategyPlanId,
    string EditorialBacklogId,
    int BacklogItemCount,
    DateTime ProcessedUtc);
