namespace AIMultiAgentPlatform.Contracts.Publishing;

public sealed record UpsertConnectedPublishingProfileResponse(
    string ConnectedPublishingProfileId,
    string ProviderName,
    string Platform,
    string ExternalProfileId,
    string DisplayName);
