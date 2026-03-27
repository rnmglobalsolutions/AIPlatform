using AIMultiAgentPlatform.Application.Abstractions.Persistence;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryInboxMessageRepository : IInboxMessageRepository
{
    private sealed record Entry(
        string MessageId,
        string ConsumerName,
        string CorrelationId,
        string TenantId,
        string PayloadJson,
        DateTime ReceivedUtc,
        DateTime? ProcessedUtc);

    private readonly Dictionary<(string MessageId, string ConsumerName), Entry> _entries = new();
    private readonly object _sync = new();

    public Task<bool> TryStartProcessingAsync(
        string messageId,
        string consumerName,
        string correlationId,
        string tenantId,
        string payloadJson,
        DateTime receivedUtc,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var key = (messageId, consumerName);
            if (_entries.ContainsKey(key))
            {
                return Task.FromResult(false);
            }

            _entries[key] = new Entry(messageId, consumerName, correlationId, tenantId, payloadJson, receivedUtc, null);
            return Task.FromResult(true);
        }
    }

    public Task MarkProcessedAsync(
        string messageId,
        string consumerName,
        DateTime processedUtc,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var key = (messageId, consumerName);
            if (_entries.TryGetValue(key, out var entry))
            {
                _entries[key] = entry with { ProcessedUtc = processedUtc };
            }
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _entries.Remove((messageId, consumerName));
        }

        return Task.CompletedTask;
    }
}
