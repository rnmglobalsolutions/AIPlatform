using AIMultiAgentPlatform.Application.Abstractions.Messaging;

namespace AIMultiAgentPlatform.Infrastructure.Messaging.InMemory;

public sealed class InMemoryCommandBus : ICommandBus
{
    public Task SendAsync(string commandName, MessageEnvelope envelope, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
