using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;

namespace AIMultiAgentPlatform.Infrastructure.Messaging.ServiceBus;

public sealed class SqlOutboxCommandEnqueuer(IOutboxMessageRepository outboxMessageRepository) : ICommandEnqueuer
{
    public Task EnqueueAsync(string commandName, MessageEnvelope envelope, CancellationToken cancellationToken = default) =>
        outboxMessageRepository.SaveAsync(
            new PendingOutboxCommand(envelope.MessageId, commandName, envelope, envelope.CreatedUtc),
            cancellationToken);
}
