using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Leads;

public sealed record LeadProfile(
    string LeadProfileId,
    TenantId TenantId,
    string ManyChatContactId,
    string FirstName,
    string LastName,
    string Email,
    string Channel,
    LeadLifecycleStage CurrentStage,
    string IntentSummary,
    string LastMessageText,
    DateTime UpdatedUtc);
