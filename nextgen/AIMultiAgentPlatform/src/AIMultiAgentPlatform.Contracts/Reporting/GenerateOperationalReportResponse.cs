namespace AIMultiAgentPlatform.Contracts.Reporting;

public sealed record GenerateOperationalReportResponse(
    string MonthlyPerformanceSnapshotId,
    string MonthKey,
    string OperationalSummary,
    int PostsPublished,
    int ApprovedContentPackages,
    int BlockedContentPackages,
    string TopPerformingAssetTitle,
    IReadOnlyList<ReportRecommendationDto> Recommendations);
