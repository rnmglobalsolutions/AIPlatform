namespace AIMultiAgentPlatform.Contracts.ManyChat;

public sealed record ProcessManyChatEventRequest(
    string TenantId,
    string ManyChatContactId,
    string EventType,
    string Channel,
    string MessageText,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    IReadOnlyList<string>? CurrentTags = null,
    IReadOnlyDictionary<string, string>? CurrentFields = null,
    string? CorrelationId = null);
