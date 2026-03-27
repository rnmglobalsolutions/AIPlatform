namespace AIMultiAgentPlatform.Contracts.Video;

public sealed record FinalizeVideoGenerationRequest(
    string TenantId,
    string VideoGenerationJobId);
