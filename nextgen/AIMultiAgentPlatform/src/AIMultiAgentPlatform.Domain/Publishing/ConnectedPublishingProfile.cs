using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Publishing;

public sealed record ConnectedPublishingProfile(
    string ConnectedPublishingProfileId,
    TenantId TenantId,
    string ProviderName,
    string Platform,
    string ExternalProfileId,
    string AccessTokenSecretReference,
    string DisplayName,
    DateTime CreatedUtc,
    DateTime UpdatedUtc)
{
    public bool HasAccessTokenSecret => !string.IsNullOrWhiteSpace(AccessTokenSecretReference);
}
