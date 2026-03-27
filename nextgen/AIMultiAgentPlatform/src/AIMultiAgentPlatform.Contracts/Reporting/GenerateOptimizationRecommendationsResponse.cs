namespace AIMultiAgentPlatform.Contracts.Reporting;

public sealed record GenerateOptimizationRecommendationsResponse(
    string MonthlyPerformanceSnapshotId,
    string MonthKey,
    IReadOnlyList<ReportRecommendationDto> Recommendations);
