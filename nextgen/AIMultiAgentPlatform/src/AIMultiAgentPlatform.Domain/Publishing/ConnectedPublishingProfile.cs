using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Publishing;

public sealed record ConnectedPublishingProfile(
    string ConnectedPublishingProfileId,
    TenantId TenantId,
    string ProviderName,
    string Platform,
    string ExternalProfileId,
    string AccessToken,
    string DisplayName,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
