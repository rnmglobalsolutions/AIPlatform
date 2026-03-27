namespace AIMultiAgentPlatform.Application.Abstractions.Messaging;

public interface IEventBus
{
    Task PublishAsync(string eventName, MessageEnvelope envelope, CancellationToken cancellationToken = default);
}
