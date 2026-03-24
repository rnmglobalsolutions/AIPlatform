using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Content;

public sealed record DailyContentRequest(
    string DailyContentRequestId,
    TenantId TenantId,
    string EditorialBacklogId,
    int EditorialBacklogSequence,
    DateTime RequestedUtc,
    string CorrelationId);
