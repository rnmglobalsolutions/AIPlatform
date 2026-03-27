namespace AIMultiAgentPlatform.Contracts.Publishing;

public sealed record PublishScheduledContentResponse(
    string SchedulingJobId,
    string Status,
    int PublishedCount,
    int FailedCount);
