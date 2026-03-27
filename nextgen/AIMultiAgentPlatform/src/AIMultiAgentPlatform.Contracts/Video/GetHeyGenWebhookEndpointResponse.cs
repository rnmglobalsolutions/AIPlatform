namespace AIMultiAgentPlatform.Contracts.Video;

public sealed record GetHeyGenWebhookEndpointResponse(
    string ProviderName,
    string EndpointId,
    string Url,
    string Status,
    IReadOnlyList<string> Events,
    bool WebhookSecretStored,
    DateTime LastSyncedUtc);
