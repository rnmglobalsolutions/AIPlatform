namespace AIMultiAgentPlatform.Domain.Content;

public sealed record VideoWebhookEndpointRegistration(
    string ProviderName,
    string EndpointId,
    string Url,
    string Status,
    IReadOnlyList<string> Events,
    string Secret,
    DateTime CreatedUtc,
    DateTime LastSyncedUtc);
