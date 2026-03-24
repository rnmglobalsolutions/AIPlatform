namespace AIMultiAgentPlatform.Contracts.Voice;

public sealed record OrchestrateVoiceAgentRequest(
    string TenantId,
    string ManyChatContactId,
    string Objective,
    string PhoneNumber,
    string? PreferredVoiceId = null,
    string? CorrelationId = null);
