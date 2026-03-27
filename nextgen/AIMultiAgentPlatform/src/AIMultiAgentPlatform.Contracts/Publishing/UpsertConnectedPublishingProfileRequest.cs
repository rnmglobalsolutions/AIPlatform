namespace AIMultiAgentPlatform.Contracts.Publishing;

public sealed record UpsertConnectedPublishingProfileRequest(
    string TenantId,
    string ProviderName,
    string Platform,
    string ExternalProfileId,
    string AccessToken,
    string DisplayName);
