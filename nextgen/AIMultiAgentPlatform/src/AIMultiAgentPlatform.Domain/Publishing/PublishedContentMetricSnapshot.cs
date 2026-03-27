using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Publishing;

public sealed record PublishedContentMetricSnapshot(
    string PublishedContentMetricSnapshotId,
    string PublishedContentRecordId,
    TenantId TenantId,
    string ProviderName,
    string Platform,
    string ProviderStatus,
    long Reach,
    long Clicks,
    long Likes,
    long Comments,
    long Shares,
    DateTime CapturedUtc);
