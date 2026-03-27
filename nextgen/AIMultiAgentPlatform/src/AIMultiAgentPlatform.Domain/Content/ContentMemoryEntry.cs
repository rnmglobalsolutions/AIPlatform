using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Content;

public sealed record ContentMemoryEntry(
    string ContentMemoryEntryId,
    TenantId TenantId,
    string SourceAssetType,
    string SourceAssetId,
    string Topic,
    string PrimaryHook,
    string CallToActionPattern,
    string LeadGoal,
    string Platform,
    string ContentHash,
    DateTime CreatedUtc,
    ContentMemoryLifecycleStage LifecycleStage = ContentMemoryLifecycleStage.Generated,
    DateTime? PublishedUtc = null,
    string SourceBacklogItemId = "",
    string SourceStrategyPlanId = "");
