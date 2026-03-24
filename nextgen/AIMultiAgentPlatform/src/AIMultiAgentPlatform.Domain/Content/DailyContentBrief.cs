using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Editorial;

namespace AIMultiAgentPlatform.Domain.Content;

public sealed record DailyContentBrief(
    string DailyContentBriefId,
    string DailyContentRequestId,
    TenantId TenantId,
    ContentCategory Category,
    PrimaryFormat PrimaryFormat,
    string Topic,
    string Angle,
    string HookDirection,
    string CoreMessage,
    string CallToActionKeyword,
    string BrandTone);
