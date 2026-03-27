using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Publishing;

public sealed record PublishedContentRecord(
    string PublishedContentRecordId,
    string DailyContentRequestId,
    string SchedulingJobId,
    TenantId TenantId,
    string ProviderName,
    string Platform,
    string ExternalProfileId,
    string ExternalPostId,
    string ExternalUrl,
    string Caption,
    string AssetUrl,
    PublishedContentStatus Status,
    string FailureReason,
    DateTime PublishedUtc);
