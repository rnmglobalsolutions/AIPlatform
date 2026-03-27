namespace AIMultiAgentPlatform.Contracts.Reporting;

public sealed record GenerateExecutiveReportRequest(
    string TenantId,
    int Year,
    int Month,
    string? CorrelationId = null);
