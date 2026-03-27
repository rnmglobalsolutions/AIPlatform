namespace AIMultiAgentPlatform.Contracts.Publishing;

public sealed record ListConnectedPublishingProfilesResponse(
    IReadOnlyList<ConnectedPublishingProfileDto> Profiles);

public sealed record ConnectedPublishingProfileDto(
    string ConnectedPublishingProfileId,
    string ProviderName,
    string Platform,
    string ExternalProfileId,
    string DisplayName);
