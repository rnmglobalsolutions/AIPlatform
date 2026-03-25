namespace AIMultiAgentPlatform.Contracts.Reporting;

public sealed record GenerateMonthlyPerformanceSnapshotResponse(
    string MonthlyPerformanceSnapshotId,
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
    double EstimatedClicks);
