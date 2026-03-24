namespace AIMultiAgentPlatform.Contracts.Content;

public sealed record GenerateDailyContentPackageRequest(
    string TenantId,
    string EditorialBacklogId,
    int Sequence,
    string? CorrelationId = null);
