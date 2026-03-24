using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Reporting;

public sealed record MonthlyPerformanceSnapshot(
    string MonthlyPerformanceSnapshotId,
    TenantId TenantId,
    string MonthKey,
    int PostsPublished,
    int VideosCreated,
    int GraphicsCreated,
    string TopPerformingAssetTitle,
    double AverageQualityScore,
    int ApprovedContentPackages,
    int BlockedContentPackages,
    int LeadsGenerated,
    int MarketingQualifiedLeads,
    int BookingReadyLeads,
    int AppointmentsBooked,
    int ReminderTouchesScheduled,
    int FollowUpTouchesScheduled,
    double EstimatedEngagement,
    double EstimatedClicks,
    DateTime GeneratedUtc);
