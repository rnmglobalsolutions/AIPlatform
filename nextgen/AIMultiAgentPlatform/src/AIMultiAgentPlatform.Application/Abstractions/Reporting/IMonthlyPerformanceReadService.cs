using AIMultiAgentPlatform.Application.Reporting;

namespace AIMultiAgentPlatform.Application.Abstractions.Reporting;

public interface IMonthlyPerformanceReadService
{
    Task<MonthlyPerformanceSource> ReadAsync(string tenantId, int year, int month, CancellationToken cancellationToken);
}
