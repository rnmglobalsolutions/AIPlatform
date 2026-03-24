using AIMultiAgentPlatform.Contracts.Voice;

namespace AIMultiAgentPlatform.Application.Voice;

public sealed record OrchestrateVoiceAgentCommand(
    OrchestrateVoiceAgentRequest Request,
    string CorrelationId);
