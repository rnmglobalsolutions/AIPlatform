namespace AIMultiAgentPlatform.Application.Abstractions.Messaging;

public sealed record MessageEnvelope(
    string MessageId,
    string CorrelationId,
    string TenantId,
    string MessageType,
    string PayloadJson,
    DateTime CreatedUtc,
    IReadOnlyDictionary<string, string>? Properties = null,
    DateTime? ScheduledEnqueueUtc = null);
