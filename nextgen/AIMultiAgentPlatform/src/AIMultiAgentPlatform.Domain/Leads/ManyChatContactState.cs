using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Leads;

public sealed record ManyChatContactState(
    string ManyChatContactStateId,
    TenantId TenantId,
    string ManyChatContactId,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string> Fields,
    string LastInboundText,
    string TriggeredFlow,
    DateTime UpdatedUtc);
