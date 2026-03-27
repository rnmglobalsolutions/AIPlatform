using AIMultiAgentPlatform.Application.Abstractions.Messaging;

namespace AIMultiAgentPlatform.Infrastructure.Messaging.InMemory;

public sealed class DirectCommandEnqueuer(ICommandBus commandBus) : ICommandEnqueuer
{
    public Task EnqueueAsync(string commandName, MessageEnvelope envelope, CancellationToken cancellationToken = default) =>
        commandBus.SendAsync(commandName, envelope, cancellationToken);
}
