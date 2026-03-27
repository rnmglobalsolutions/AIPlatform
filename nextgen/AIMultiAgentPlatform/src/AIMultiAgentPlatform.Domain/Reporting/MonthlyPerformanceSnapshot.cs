using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Reporting;

public sealed record MonthlyPerformanceSnapshot(
    string MonthlyPerformanceSnapshotId,
    TenantId TenantId,
    string MonthKey,
    string ContentLanguage,
    string PrimaryConversionAction,
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
    DateTime GeneratedUtc,
    long TotalReach = 0,
    long TotalClicks = 0,
    long TotalLikes = 0,
    long TotalComments = 0,
    long TotalShares = 0,
    int AttributedLeads = 0,
    int AttributedBookings = 0);
