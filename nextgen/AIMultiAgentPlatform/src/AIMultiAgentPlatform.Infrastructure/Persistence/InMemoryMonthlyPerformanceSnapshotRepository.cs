using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Reporting;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryMonthlyPerformanceSnapshotRepository : IMonthlyPerformanceSnapshotRepository
{
    private readonly ConcurrentDictionary<string, MonthlyPerformanceSnapshot> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(MonthlyPerformanceSnapshot snapshot, CancellationToken cancellationToken)
    {
        _items[BuildKey(snapshot.TenantId.Value, snapshot.MonthKey)] = snapshot;
        return Task.CompletedTask;
    }

    public Task<MonthlyPerformanceSnapshot?> FindAsync(string tenantId, string monthKey, CancellationToken cancellationToken) =>
        Task.FromResult(Find(tenantId, monthKey));

    public MonthlyPerformanceSnapshot? Find(string tenantId, string monthKey) =>
        _items.TryGetValue(BuildKey(tenantId, monthKey), out var snapshot) ? snapshot : null;

    private static string BuildKey(string tenantId, string monthKey) => $"{tenantId}::{monthKey}";
}
