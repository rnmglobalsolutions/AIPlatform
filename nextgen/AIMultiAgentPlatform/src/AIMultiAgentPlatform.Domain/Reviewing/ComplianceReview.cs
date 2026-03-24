using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Reviewing;

public sealed record ComplianceReview(
    string ComplianceReviewId,
    string DailyContentRequestId,
    TenantId TenantId,
    RiskLevel RiskLevel,
    IReadOnlyList<ComplianceIssue> Issues,
    string SafeVersionSummary,
    DateTime ReviewedUtc);
