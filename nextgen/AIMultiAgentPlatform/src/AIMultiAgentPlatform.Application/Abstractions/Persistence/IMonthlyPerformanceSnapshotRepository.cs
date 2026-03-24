using AIMultiAgentPlatform.Domain.Reporting;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IMonthlyPerformanceSnapshotRepository
{
    Task SaveAsync(MonthlyPerformanceSnapshot snapshot, CancellationToken cancellationToken);

    Task<MonthlyPerformanceSnapshot?> FindAsync(string tenantId, string monthKey, CancellationToken cancellationToken);
}
