namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IInboxMessageRepository
{
    Task<bool> TryStartProcessingAsync(
        string messageId,
        string consumerName,
        string correlationId,
        string tenantId,
        string payloadJson,
        DateTime receivedUtc,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        string messageId,
        string consumerName,
        DateTime processedUtc,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken);
}
