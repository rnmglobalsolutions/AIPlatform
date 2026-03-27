using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryContentMemoryRepository : IContentMemoryRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ContentMemoryEntry>> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(ContentMemoryEntry entry, CancellationToken cancellationToken)
    {
        var tenantId = NormalizeTenantId(entry.TenantId.Value);
        var tenantEntries = _items.GetOrAdd(tenantId, static _ => new ConcurrentDictionary<string, ContentMemoryEntry>(StringComparer.Ordinal));
        tenantEntries[entry.ContentMemoryEntryId] = entry;
        return Task.CompletedTask;
    }

    public Task<ContentMemorySnapshot> GetSnapshotAsync(string tenantId, int maxEntries, CancellationToken cancellationToken)
    {
        var normalizedTenantId = NormalizeTenantId(tenantId);
        var normalizedMaxEntries = Math.Clamp(maxEntries, 1, 100);
        if (!_items.TryGetValue(normalizedTenantId, out var tenantEntries) || tenantEntries.IsEmpty)
        {
            return Task.FromResult(ContentMemorySnapshot.Empty(new TenantId(normalizedTenantId), DateTime.UtcNow));
        }

        var entries = tenantEntries.Values
            .OrderByDescending(static entry => entry.PublishedUtc ?? entry.CreatedUtc)
            .ThenByDescending(static entry => entry.CreatedUtc)
            .Take(normalizedMaxEntries)
            .ToArray();

        return Task.FromResult(BuildSnapshot(new TenantId(normalizedTenantId), entries, DateTime.UtcNow));
    }

    public ContentMemoryEntry? Find(string tenantId, string contentMemoryEntryId) =>
        _items.TryGetValue(tenantId.Trim(), out var tenantEntries) &&
        tenantEntries.TryGetValue(contentMemoryEntryId, out var entry)
            ? entry
            : null;

    private static ContentMemorySnapshot BuildSnapshot(
        TenantId tenantId,
        IReadOnlyList<ContentMemoryEntry> entries,
        DateTime generatedUtc)
    {
        return new ContentMemorySnapshot(
            tenantId,
            generatedUtc,
            entries,
            DistinctNonEmpty(entries.Select(static entry => entry.Topic)),
            DistinctNonEmpty(entries.Select(static entry => entry.PrimaryHook)),
            DistinctNonEmpty(entries.Select(static entry => entry.CallToActionPattern)),
            DistinctNonEmpty(entries.Select(static entry => entry.Platform)),
            DistinctNonEmpty(entries.Select(static entry => entry.LeadGoal)),
            DistinctNonEmpty(entries.Select(static entry => entry.ContentHash)));
    }

    private static IReadOnlyList<string> DistinctNonEmpty(IEnumerable<string> values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizeTenantId(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId is required when saving or loading content memory.", nameof(tenantId));
        }

        return tenantId.Trim();
    }
}
