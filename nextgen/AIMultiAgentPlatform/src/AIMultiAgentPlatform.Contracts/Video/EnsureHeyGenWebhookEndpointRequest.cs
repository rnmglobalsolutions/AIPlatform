namespace AIMultiAgentPlatform.Contracts.Video;

public sealed record EnsureHeyGenWebhookEndpointRequest(
    string? PublicWebhookUrl = null,
    IReadOnlyList<string>? Events = null);
