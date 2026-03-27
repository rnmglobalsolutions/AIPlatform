namespace AIMultiAgentPlatform.Contracts.Publishing;

public sealed record ReconcilePublishedContentRequest(
    string TenantId,
    string SchedulingJobId,
    string? CorrelationId = null);
