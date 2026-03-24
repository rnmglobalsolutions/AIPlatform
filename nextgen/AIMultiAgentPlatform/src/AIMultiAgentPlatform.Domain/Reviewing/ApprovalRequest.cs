using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Reviewing;

public sealed record ApprovalRequest(
    string ApprovalRequestId,
    string DailyContentRequestId,
    TenantId TenantId,
    ApprovalStatus Status,
    string DecisionSummary,
    DateTime ReviewedUtc);
