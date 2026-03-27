namespace AIMultiAgentPlatform.Contracts.Video;

public sealed record EnsureHeyGenWebhookEndpointResponse(
    string Outcome,
    string ProviderName,
    string EndpointId,
    string Url,
    string Status,
    IReadOnlyList<string> Events,
    bool WebhookSecretStored);
