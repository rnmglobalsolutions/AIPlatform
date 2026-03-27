using AIMultiAgentPlatform.Application.Abstractions.Messaging;

namespace AIMultiAgentPlatform.Infrastructure.Messaging.InMemory;

public sealed class InMemoryEventBus : IEventBus
{
    public Task PublishAsync(string eventName, MessageEnvelope envelope, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
