namespace AIMultiAgentPlatform.Application.Abstractions.Messaging;

public interface ICommandEnqueuer
{
    Task EnqueueAsync(string commandName, MessageEnvelope envelope, CancellationToken cancellationToken = default);
}
