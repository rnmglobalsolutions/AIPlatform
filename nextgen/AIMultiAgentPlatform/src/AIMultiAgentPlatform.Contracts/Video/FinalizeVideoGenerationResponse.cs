namespace AIMultiAgentPlatform.Contracts.Video;

public sealed record FinalizeVideoGenerationResponse(
    string VideoGenerationJobId,
    string Status,
    string GeneratedVideoAssetId,
    string VideoUrl);
