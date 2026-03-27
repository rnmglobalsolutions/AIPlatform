using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Content;

public sealed record GeneratedVideoAsset(
    string GeneratedVideoAssetId,
    string VideoGenerationJobId,
    string DailyContentRequestId,
    TenantId TenantId,
    string PrimaryAssetId,
    string ProviderName,
    string ProviderJobId,
    string Title,
    string ProviderVideoUrl,
    string VideoUrl,
    string TranscriptText,
    string Language,
    string AspectRatio,
    DateTime CreatedUtc,
    IReadOnlyList<TimedTranscriptSegment>? TranscriptSegments = null);
