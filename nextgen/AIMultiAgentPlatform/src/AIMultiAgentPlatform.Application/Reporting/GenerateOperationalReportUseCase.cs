using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Reporting;

namespace AIMultiAgentPlatform.Application.Reporting;

public sealed class GenerateOperationalReportUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IMonthlyPerformanceReadService _monthlyPerformanceReadService;
    private readonly IMonthlyPerformanceSnapshotRepository _monthlyPerformanceSnapshotRepository;
    private readonly GenerateMonthlyPerformanceSnapshotUseCase _generateMonthlyPerformanceSnapshotUseCase;
    private readonly IReportAgent _reportAgent;

    public GenerateOperationalReportUseCase(
        ITenantRepository tenantRepository,
        IMonthlyPerformanceReadService monthlyPerformanceReadService,
        IMonthlyPerformanceSnapshotRepository monthlyPerformanceSnapshotRepository,
        GenerateMonthlyPerformanceSnapshotUseCase generateMonthlyPerformanceSnapshotUseCase,
        IReportAgent reportAgent)
    {
        _tenantRepository = tenantRepository;
        _monthlyPerformanceReadService = monthlyPerformanceReadService;
        _monthlyPerformanceSnapshotRepository = monthlyPerformanceSnapshotRepository;
        _generateMonthlyPerformanceSnapshotUseCase = generateMonthlyPerformanceSnapshotUseCase;
        _reportAgent = reportAgent;
    }

    public async Task<Result<GenerateOperationalReportResponse>> ExecuteAsync(
        GenerateOperationalReportRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<GenerateOperationalReportResponse>.Failure("report.tenant.not-found", "Tenant was not found.");
        }

        var monthKey = $"{request.Year:D4}-{request.Month:D2}";
        var snapshot = await _monthlyPerformanceSnapshotRepository.FindAsync(request.TenantId, monthKey, cancellationToken);
        if (snapshot is null)
        {
            var generated = await _generateMonthlyPerformanceSnapshotUseCase.ExecuteAsync(
                new GenerateMonthlyPerformanceSnapshotCommand(
                    new GenerateMonthlyPerformanceSnapshotRequest(request.TenantId, request.Year, request.Month, request.CorrelationId),
                    request.CorrelationId ?? string.Empty),
                cancellationToken);
            if (!generated.IsSuccess)
            {
                return Result<GenerateOperationalReportResponse>.Failure(generated.ErrorCode ?? "report.snapshot.failed", generated.ErrorMessage ?? "Monthly snapshot could not be generated.");
            }

            snapshot = await _monthlyPerformanceSnapshotRepository.FindAsync(request.TenantId, monthKey, cancellationToken);
            if (snapshot is null)
            {
                return Result<GenerateOperationalReportResponse>.Failure("report.snapshot.not-found", "Monthly snapshot could not be loaded.");
            }
        }

        var source = await _monthlyPerformanceReadService.ReadAsync(request.TenantId, request.Year, request.Month, cancellationToken);
        var report = await _reportAgent.GenerateAsync(new ReportGenerationContext(tenant, snapshot, source), cancellationToken);

        return Result<GenerateOperationalReportResponse>.Success(
            new GenerateOperationalReportResponse(
                snapshot.MonthlyPerformanceSnapshotId,
                snapshot.MonthKey,
                report.OperationalSummary,
                snapshot.PostsPublished,
                snapshot.ApprovedContentPackages,
                snapshot.BlockedContentPackages,
                snapshot.TopPerformingAssetTitle,
                report.Recommendations.Select(ToDto).ToArray()));
    }

    private static ReportRecommendationDto ToDto(Domain.Reporting.ReportRecommendation recommendation) =>
        new(
            recommendation.Title,
            recommendation.Priority,
            recommendation.Rationale,
            recommendation.RecommendedAction,
            recommendation.SupportingMetric);
}
