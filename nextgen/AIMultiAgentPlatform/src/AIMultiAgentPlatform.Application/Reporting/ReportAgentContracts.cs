using AIMultiAgentPlatform.Domain.Reporting;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Reporting;

public sealed record ReportGenerationContext(
    Tenant Tenant,
    MonthlyPerformanceSnapshot Snapshot,
    MonthlyPerformanceSource Source);

public sealed record ReportAgentResult(
    string ExecutiveSummary,
    string OperationalSummary,
    IReadOnlyList<ReportRecommendation> Recommendations);
