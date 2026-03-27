namespace AIMultiAgentPlatform.Application.Abstractions.Messaging;

public sealed record PendingOutboxCommand(
    string OutboxMessageId,
    string CommandName,
    MessageEnvelope Envelope,
    DateTime CreatedUtc);

public sealed record DispatchOutboxCommandsResult(
    int AttemptedCount,
    int DispatchedCount);
