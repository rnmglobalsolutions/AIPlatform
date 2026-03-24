namespace AIMultiAgentPlatform.Contracts.Content;

public sealed record ReviewAndScheduleDailyContentRequest(
    string TenantId,
    string DailyContentRequestId,
    string? CorrelationId = null);
