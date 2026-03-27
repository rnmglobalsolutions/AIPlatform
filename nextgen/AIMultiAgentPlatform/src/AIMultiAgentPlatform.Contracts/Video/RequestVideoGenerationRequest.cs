namespace AIMultiAgentPlatform.Contracts.Video;

public sealed record RequestVideoGenerationRequest(
    string TenantId,
    string DailyContentRequestId,
    string ProviderProfile = "default",
    string AspectRatio = "9:16",
    string CorrelationId = "");
