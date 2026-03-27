namespace AIMultiAgentPlatform.Contracts.Reporting;

public sealed record GenerateOptimizationRecommendationsRequest(
    string TenantId,
    int Year,
    int Month,
    string? CorrelationId = null);
