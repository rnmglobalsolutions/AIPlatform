namespace AIMultiAgentPlatform.Application.Abstractions.Messaging;

public interface ICommandBus
{
    Task SendAsync(string commandName, MessageEnvelope envelope, CancellationToken cancellationToken = default);
}
