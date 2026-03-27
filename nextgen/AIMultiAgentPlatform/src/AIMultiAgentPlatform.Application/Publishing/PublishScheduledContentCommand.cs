using AIMultiAgentPlatform.Contracts.Publishing;

namespace AIMultiAgentPlatform.Application.Publishing;

public sealed record PublishScheduledContentCommand(
    PublishScheduledContentRequest Request,
    string CorrelationId = "");
