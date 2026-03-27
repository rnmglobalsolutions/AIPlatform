namespace AIMultiAgentPlatform.Application.Abstractions.Video;

public interface IWebhookEndpointManager
{
    Task<WebhookEndpointListResult> ListAsync(CancellationToken cancellationToken);

    Task<WebhookEndpointMutationResult> CreateAsync(
        string url,
        IReadOnlyList<string> events,
        CancellationToken cancellationToken);

    Task<WebhookEndpointMutationResult> UpdateAsync(
        string endpointId,
        string url,
        IReadOnlyList<string> events,
        CancellationToken cancellationToken);

    Task<WebhookEndpointDeletionResult> DeleteAsync(
        string endpointId,
        CancellationToken cancellationToken);
}
