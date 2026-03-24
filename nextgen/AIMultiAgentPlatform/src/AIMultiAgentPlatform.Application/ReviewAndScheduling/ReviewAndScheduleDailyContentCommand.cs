using AIMultiAgentPlatform.Contracts.Content;

namespace AIMultiAgentPlatform.Application.ReviewAndScheduling;

public sealed record ReviewAndScheduleDailyContentCommand(
    ReviewAndScheduleDailyContentRequest Request,
    string CorrelationId = "");
