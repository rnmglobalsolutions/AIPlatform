namespace AIMultiAgentPlatform.Application.Abstractions.Publishing;

public sealed record PublishingAccessTokenSecret(
    string SecretReference,
    string TenantId,
    string ProviderName,
    string Platform,
    string AccessToken,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
