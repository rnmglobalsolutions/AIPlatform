namespace AIMultiAgentPlatform.Application.Abstractions.Video;

public sealed record WebhookEndpointDescriptor(
    string ProviderName,
    string EndpointId,
    string Url,
    string Status,
    IReadOnlyList<string> Events,
    string Secret,
    DateTime? CreatedUtc);

public sealed record WebhookEndpointListResult(
    bool Succeeded,
    IReadOnlyList<WebhookEndpointDescriptor> Endpoints,
    string FailureReason)
{
    public static WebhookEndpointListResult Success(IReadOnlyList<WebhookEndpointDescriptor> endpoints) =>
        new(true, endpoints, string.Empty);

    public static WebhookEndpointListResult Failure(string failureReason) =>
        new(false, [], failureReason);
}

public sealed record WebhookEndpointMutationResult(
    bool Succeeded,
    WebhookEndpointDescriptor? Endpoint,
    string FailureReason)
{
    public static WebhookEndpointMutationResult Success(WebhookEndpointDescriptor endpoint) =>
        new(true, endpoint, string.Empty);

    public static WebhookEndpointMutationResult Failure(string failureReason) =>
        new(false, null, failureReason);
}

public sealed record WebhookEndpointDeletionResult(
    bool Succeeded,
    string FailureReason)
{
    public static WebhookEndpointDeletionResult Success() => new(true, string.Empty);

    public static WebhookEndpointDeletionResult Failure(string failureReason) =>
        new(false, failureReason);
}
