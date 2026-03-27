namespace AIMultiAgentPlatform.Contracts.Video;

public sealed record RequestVideoGenerationResponse(
    string VideoGenerationJobId,
    string ProviderName,
    string ProviderJobId,
    string Status);
