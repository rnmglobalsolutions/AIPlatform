using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.FollowUps;

public sealed record FollowUpSequence(
    string FollowUpSequenceId,
    TenantId TenantId,
    string LeadProfileId,
    FollowUpSequenceStatus Status,
    string Reason,
    IReadOnlyList<FollowUpStep> Steps,
    DateTime CreatedUtc);
