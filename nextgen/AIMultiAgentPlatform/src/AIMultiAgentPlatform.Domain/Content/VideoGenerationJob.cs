using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Content;

public sealed record VideoGenerationJob(
    string VideoGenerationJobId,
    string DailyContentRequestId,
    TenantId TenantId,
    string PrimaryAssetId,
    string ProviderName,
    string ProviderProfile,
    string ProviderJobId,
    string Title,
    string Script,
    string Language,
    string AspectRatio,
    VideoGenerationJobStatus Status,
    string FailureReason,
    DateTime RequestedUtc,
    DateTime? LastCheckedUtc = null,
    DateTime? CompletedUtc = null);
