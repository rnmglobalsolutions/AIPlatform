using AIMultiAgentPlatform.Application.Abstractions.Messaging;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IOutboxMessageRepository
{
    Task SaveAsync(PendingOutboxCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingOutboxCommand>> GetPendingAsync(int maxCount, CancellationToken cancellationToken);

    Task MarkDispatchedAsync(string outboxMessageId, DateTime dispatchedUtc, CancellationToken cancellationToken);
}
