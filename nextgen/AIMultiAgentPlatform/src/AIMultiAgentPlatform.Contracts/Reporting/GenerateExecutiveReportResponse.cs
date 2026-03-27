namespace AIMultiAgentPlatform.Contracts.Reporting;

public sealed record GenerateExecutiveReportResponse(
    string MonthlyPerformanceSnapshotId,
    string MonthKey,
    string ExecutiveSummary,
    long TotalReach,
    long TotalClicks,
    int AttributedLeads,
    int AttributedBookings,
    IReadOnlyList<ReportRecommendationDto> Recommendations);
