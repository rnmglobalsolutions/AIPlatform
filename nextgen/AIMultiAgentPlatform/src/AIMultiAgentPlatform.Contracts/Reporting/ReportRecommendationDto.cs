namespace AIMultiAgentPlatform.Contracts.Reporting;

public sealed record ReportRecommendationDto(
    string Title,
    string Priority,
    string Rationale,
    string RecommendedAction,
    string SupportingMetric);
