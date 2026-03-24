using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Reporting;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Reporting;
using AIMultiAgentPlatform.Domain.Reviewing;

namespace AIMultiAgentPlatform.Application.Reporting;

public sealed class GenerateMonthlyPerformanceSnapshotUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IMonthlyPerformanceReadService _monthlyPerformanceReadService;
    private readonly IMonthlyPerformanceSnapshotRepository _monthlyPerformanceSnapshotRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public GenerateMonthlyPerformanceSnapshotUseCase(
        ITenantRepository tenantRepository,
        IMonthlyPerformanceReadService monthlyPerformanceReadService,
        IMonthlyPerformanceSnapshotRepository monthlyPerformanceSnapshotRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _monthlyPerformanceReadService = monthlyPerformanceReadService;
        _monthlyPerformanceSnapshotRepository = monthlyPerformanceSnapshotRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<GenerateMonthlyPerformanceSnapshotResponse>> ExecuteAsync(
        GenerateMonthlyPerformanceSnapshotCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<GenerateMonthlyPerformanceSnapshotResponse>.Failure("report.tenant.required", "TenantId is required.");
        }

        if (request.Month is < 1 or > 12)
        {
            return Result<GenerateMonthlyPerformanceSnapshotResponse>.Failure("report.month.invalid", "Month must be between 1 and 12.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<GenerateMonthlyPerformanceSnapshotResponse>.Failure("report.tenant.not-found", "Tenant was not found.");
        }

        var source = await _monthlyPerformanceReadService.ReadAsync(request.TenantId, request.Year, request.Month, cancellationToken);
        var monthKey = $"{request.Year:D4}-{request.Month:D2}";

        var videosCreated = source.PrimaryAssets.Count(asset => asset.PrimaryFormat == PrimaryFormat.ShortVideo);
        var graphicsCreated = source.PrimaryAssets.Count(asset => asset.PrimaryFormat == PrimaryFormat.BrandedGraphic);
        var postsPublished = source.SchedulingJobs
            .Where(job => job.Status == Domain.Publishing.SchedulingStatus.Scheduled)
            .Sum(job => job.Targets.Count);

        var approvedContentPackages = source.ApprovalRequests.Count(item => item.Status == ApprovalStatus.Approved);
        var blockedContentPackages = source.ApprovalRequests.Count(item => item.Status == ApprovalStatus.NeedsChanges);
        var averageQualityScore = source.QualityReviews.Count == 0
            ? 0
            : Math.Round(source.QualityReviews.Average(item => item.OverallScore), 2);

        var topPerformer = ResolveTopPerformer(source);
        var leadsGenerated = source.LeadProfiles.Count;
        var marketingQualifiedLeads = source.LeadProfiles.Count(item => item.CurrentStage >= LeadLifecycleStage.MarketingQualified);
        var bookingReadyLeads = source.LeadProfiles.Count(item => item.CurrentStage >= LeadLifecycleStage.BookingReady);
        var appointmentsBooked = source.BookingRecords.Count(item => item.Status == Domain.Booking.BookingStatus.Booked);
        var reminderTouchesScheduled = source.ReminderSchedules.Sum(item => item.Touches.Count);
        var followUpTouchesScheduled = source.FollowUpSequences.Sum(item => item.Steps.Count);

        var estimatedEngagement = Math.Round((averageQualityScore * Math.Max(postsPublished, 1)) + (marketingQualifiedLeads * 3.5), 2);
        var estimatedClicks = Math.Round((appointmentsBooked * 4.0) + (marketingQualifiedLeads * 1.25), 2);

        var snapshot = new MonthlyPerformanceSnapshot(
            _idGenerator.NewId("monthly_snapshot"),
            tenant.TenantId,
            monthKey,
            postsPublished,
            videosCreated,
            graphicsCreated,
            topPerformer,
            averageQualityScore,
            approvedContentPackages,
            blockedContentPackages,
            leadsGenerated,
            marketingQualifiedLeads,
            bookingReadyLeads,
            appointmentsBooked,
            reminderTouchesScheduled,
            followUpTouchesScheduled,
            estimatedEngagement,
            estimatedClicks,
            _clock.UtcNow);

        await _monthlyPerformanceSnapshotRepository.SaveAsync(snapshot, cancellationToken);

        return Result<GenerateMonthlyPerformanceSnapshotResponse>.Success(
            new GenerateMonthlyPerformanceSnapshotResponse(
                snapshot.MonthlyPerformanceSnapshotId,
                snapshot.MonthKey,
                snapshot.PostsPublished,
                snapshot.VideosCreated,
                snapshot.GraphicsCreated,
                snapshot.TopPerformingAssetTitle,
                snapshot.AverageQualityScore,
                snapshot.ApprovedContentPackages,
                snapshot.BlockedContentPackages,
                snapshot.LeadsGenerated,
                snapshot.MarketingQualifiedLeads,
                snapshot.BookingReadyLeads,
                snapshot.AppointmentsBooked,
                snapshot.ReminderTouchesScheduled,
                snapshot.FollowUpTouchesScheduled,
                snapshot.EstimatedEngagement,
                snapshot.EstimatedClicks));
    }

    private static string ResolveTopPerformer(MonthlyPerformanceSource source)
    {
        if (source.PrimaryAssets.Count == 0)
        {
            return "No content generated";
        }

        var scoreByRequestId = source.QualityReviews.ToDictionary(item => item.DailyContentRequestId, item => item.OverallScore, StringComparer.Ordinal);

        return source.PrimaryAssets
            .OrderByDescending(asset => scoreByRequestId.TryGetValue(asset.DailyContentRequestId, out var score) ? score : 0)
            .ThenBy(asset => asset.Headline, StringComparer.Ordinal)
            .Select(asset => asset.Headline)
            .First();
    }
}
