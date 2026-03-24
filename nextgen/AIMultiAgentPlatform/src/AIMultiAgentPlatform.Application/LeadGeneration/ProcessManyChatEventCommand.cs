using AIMultiAgentPlatform.Contracts.ManyChat;

namespace AIMultiAgentPlatform.Application.LeadGeneration;

public sealed record ProcessManyChatEventCommand(
    ProcessManyChatEventRequest Request,
    string CorrelationId = "");
