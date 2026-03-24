using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Editorial;

namespace AIMultiAgentPlatform.Domain.Content;

public sealed record PrimaryAsset(
    string PrimaryAssetId,
    string DailyContentRequestId,
    TenantId TenantId,
    PrimaryFormat PrimaryFormat,
    string Headline,
    string Hook,
    string Body,
    string Payoff,
    string CallToAction,
    string ProductionNotes);
