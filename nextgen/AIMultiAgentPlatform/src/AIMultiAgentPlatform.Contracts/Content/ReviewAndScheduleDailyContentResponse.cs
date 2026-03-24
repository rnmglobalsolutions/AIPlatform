namespace AIMultiAgentPlatform.Contracts.Content;

public sealed record ReviewAndScheduleDailyContentResponse(
    string ComplianceReviewId,
    string QualityReviewId,
    string ApprovalRequestId,
    string SchedulingJobId,
    string RiskLevel,
    double OverallQualityScore,
    string ApprovalStatus,
    string SchedulingStatus,
    int ScheduledTargetCount);
