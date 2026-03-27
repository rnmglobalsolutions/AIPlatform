using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Content;

public sealed record ContentMemorySnapshot(
    TenantId TenantId,
    DateTime GeneratedUtc,
    IReadOnlyList<ContentMemoryEntry> Entries,
    IReadOnlyList<string> RecentTopics,
    IReadOnlyList<string> RecentHooks,
    IReadOnlyList<string> RecentCallToActionPatterns,
    IReadOnlyList<string> RecentPlatforms,
    IReadOnlyList<string> RecentLeadGoals,
    IReadOnlyList<string> RecentContentHashes)
{
    public IReadOnlyList<ContentMemoryEntry> GeneratedEntries =>
        Entries.Where(static entry => entry.LifecycleStage == ContentMemoryLifecycleStage.Generated).ToArray();

    public IReadOnlyList<ContentMemoryEntry> PublishedEntries =>
        Entries.Where(static entry => entry.LifecycleStage == ContentMemoryLifecycleStage.Published).ToArray();

    public IReadOnlyList<string> RecentGeneratedTopics =>
        DistinctNonEmpty(GeneratedEntries.Select(static entry => entry.Topic));

    public IReadOnlyList<string> RecentPublishedTopics =>
        DistinctNonEmpty(PublishedEntries.Select(static entry => entry.Topic));

    public IReadOnlyList<string> RecentGeneratedHooks =>
        DistinctNonEmpty(GeneratedEntries.Select(static entry => entry.PrimaryHook));

    public IReadOnlyList<string> RecentPublishedHooks =>
        DistinctNonEmpty(PublishedEntries.Select(static entry => entry.PrimaryHook));

    public static ContentMemorySnapshot Empty(TenantId tenantId, DateTime generatedUtc) =>
        new(
            tenantId,
            generatedUtc,
            Array.Empty<ContentMemoryEntry>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    private static IReadOnlyList<string> DistinctNonEmpty(IEnumerable<string> values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
