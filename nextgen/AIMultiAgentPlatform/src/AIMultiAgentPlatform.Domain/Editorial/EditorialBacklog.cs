using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Editorial;

public sealed record EditorialBacklog(
    string EditorialBacklogId,
    TenantId TenantId,
    int WindowDays,
    DateTime SeededUtc,
    IReadOnlyList<EditorialBacklogItem> Items);
