namespace AIMultiAgentPlatform.Contracts.Reporting;

public sealed record GenerateMonthlyPerformanceSnapshotRequest(
    string TenantId,
    int Year,
    int Month,
    string? CorrelationId = null);
