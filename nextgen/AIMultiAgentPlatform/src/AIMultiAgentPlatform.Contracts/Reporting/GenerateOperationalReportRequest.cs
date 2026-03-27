namespace AIMultiAgentPlatform.Contracts.Reporting;

public sealed record GenerateOperationalReportRequest(
    string TenantId,
    int Year,
    int Month,
    string? CorrelationId = null);
