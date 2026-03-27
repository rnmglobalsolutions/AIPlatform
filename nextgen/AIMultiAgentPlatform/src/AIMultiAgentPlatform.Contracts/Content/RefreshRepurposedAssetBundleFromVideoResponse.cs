namespace AIMultiAgentPlatform.Contracts.Content;

public sealed record RefreshRepurposedAssetBundleFromVideoResponse(
    string RepurposedAssetBundleId,
    string GeneratedVideoAssetId,
    int TranscriptSentenceCount);
