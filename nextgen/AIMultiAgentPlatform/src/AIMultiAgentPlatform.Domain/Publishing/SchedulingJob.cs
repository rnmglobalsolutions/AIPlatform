using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Publishing;

public sealed record SchedulingJob(
    string SchedulingJobId,
    string DailyContentRequestId,
    TenantId TenantId,
    SchedulingStatus Status,
    string DecisionReason,
    DateTime CreatedUtc,
    IReadOnlyList<PublicationTarget> Targets);
