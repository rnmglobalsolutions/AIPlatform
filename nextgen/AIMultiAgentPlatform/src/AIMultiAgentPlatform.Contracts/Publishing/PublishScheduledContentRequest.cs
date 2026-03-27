namespace AIMultiAgentPlatform.Contracts.Publishing;

public sealed record PublishScheduledContentRequest(
    string TenantId,
    string SchedulingJobId);
