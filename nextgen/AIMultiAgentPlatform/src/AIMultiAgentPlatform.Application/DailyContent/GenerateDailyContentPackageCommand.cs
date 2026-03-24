using AIMultiAgentPlatform.Contracts.Content;

namespace AIMultiAgentPlatform.Application.DailyContent;

public sealed record GenerateDailyContentPackageCommand(
    GenerateDailyContentPackageRequest Request,
    string CorrelationId = "");
