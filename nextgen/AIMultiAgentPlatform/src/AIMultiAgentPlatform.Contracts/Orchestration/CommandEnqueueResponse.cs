namespace AIMultiAgentPlatform.Contracts.Orchestration;

public sealed record CommandEnqueueResponse(
    string MessageId,
    string CorrelationId,
    string CommandName,
    string TenantId,
    DateTime AcceptedUtc);
