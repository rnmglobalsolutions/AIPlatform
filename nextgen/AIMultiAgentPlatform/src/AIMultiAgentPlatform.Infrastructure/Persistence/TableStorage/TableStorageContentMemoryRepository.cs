using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Azure.Data.Tables;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.TableStorage;

internal sealed class TableStorageContentMemoryRepository : IContentMemoryRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TableClient _tableClient;

    public TableStorageContentMemoryRepository(TableServiceClient tableServiceClient, TableStorageOptions options)
    {
        _tableClient = tableServiceClient.GetTableClient(options.ContentMemoryTableName);
    }

    public async Task SaveAsync(ContentMemoryEntry entry, CancellationToken cancellationToken)
    {
        var tenantId = NormalizeTenantId(entry.TenantId.Value);
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var entity = new TableEntity(tenantId, BuildRowKey(entry))
        {
            ["PayloadJson"] = JsonSerializer.Serialize(entry, SerializerOptions),
            ["EffectiveUtc"] = entry.PublishedUtc ?? entry.CreatedUtc
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<ContentMemorySnapshot> GetSnapshotAsync(string tenantId, int maxEntries, CancellationToken cancellationToken)
    {
        var normalizedTenantId = NormalizeTenantId(tenantId);
        var normalizedMaxEntries = Math.Clamp(maxEntries, 1, 100);
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var entries = new List<ContentMemoryEntry>(normalizedMaxEntries);
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                           entity => entity.PartitionKey == normalizedTenantId,
                           maxPerPage: normalizedMaxEntries,
                           cancellationToken: cancellationToken))
        {
            if (entity.TryGetValue("PayloadJson", out var payloadValue) &&
                payloadValue is string payloadJson)
            {
                var entry = JsonSerializer.Deserialize<ContentMemoryEntry>(payloadJson, SerializerOptions);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count >= normalizedMaxEntries)
            {
                break;
            }
        }

        if (entries.Count == 0)
        {
            return ContentMemorySnapshot.Empty(new TenantId(normalizedTenantId), DateTime.UtcNow);
        }

        return new ContentMemorySnapshot(
            new TenantId(normalizedTenantId),
            DateTime.UtcNow,
            entries,
            DistinctNonEmpty(entries.Select(static entry => entry.Topic)),
            DistinctNonEmpty(entries.Select(static entry => entry.PrimaryHook)),
            DistinctNonEmpty(entries.Select(static entry => entry.CallToActionPattern)),
            DistinctNonEmpty(entries.Select(static entry => entry.Platform)),
            DistinctNonEmpty(entries.Select(static entry => entry.LeadGoal)),
            DistinctNonEmpty(entries.Select(static entry => entry.ContentHash)));
    }

    private static string BuildRowKey(ContentMemoryEntry entry)
    {
        var effectiveUtc = entry.PublishedUtc ?? entry.CreatedUtc;
        var inverseTicks = DateTime.MaxValue.Ticks - effectiveUtc.Ticks;
        return $"{inverseTicks:D19}_{entry.ContentMemoryEntryId}";
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
