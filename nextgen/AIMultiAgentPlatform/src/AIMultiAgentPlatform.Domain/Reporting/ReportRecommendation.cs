namespace AIMultiAgentPlatform.Domain.Reporting;

public sealed record ReportRecommendation(
    string Title,
    string Priority,
    string Rationale,
    string RecommendedAction,
    string SupportingMetric);
