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
        var postsPublished = source.PublishedContentRecords.Count > 0
            ? source.PublishedContentRecords.Count(record => record.Status == Domain.Publishing.PublishedContentStatus.Published)
            : source.SchedulingJobs
                .Where(job =>
                    job.Status == Domain.Publishing.SchedulingStatus.Published ||
                    job.Status == Domain.Publishing.SchedulingStatus.Scheduled)
                .Sum(job => job.Targets.Count);

        var approvedContentPackages = source.ApprovalRequests.Count(item => item.Status == ApprovalStatus.Approved);
        var blockedContentPackages = source.ApprovalRequests.Count(item => item.Status == ApprovalStatus.NeedsChanges);
        var averageQualityScore = source.QualityReviews.Count == 0
            ? 0
            : Math.Round(source.QualityReviews.Average(item => item.OverallScore), 2);

        var latestMetricSnapshots = SelectLatestMetricSnapshots(source);
        var totalReach = latestMetricSnapshots.Sum(item => item.Reach);
        var totalClicks = latestMetricSnapshots.Sum(item => item.Clicks);
        var totalLikes = latestMetricSnapshots.Sum(item => item.Likes);
        var totalComments = latestMetricSnapshots.Sum(item => item.Comments);
        var totalShares = latestMetricSnapshots.Sum(item => item.Shares);
        var attributedRecordIds = source.PublishedContentRecords
            .Select(item => item.PublishedContentRecordId)
            .ToHashSet(StringComparer.Ordinal);
        var attributedLeads = source.LeadProfiles.Count(item =>
            !string.IsNullOrWhiteSpace(item.SourcePublishedContentRecordId) &&
            attributedRecordIds.Contains(item.SourcePublishedContentRecordId));
        var attributedBookings = source.BookingRecords.Count(item =>
            !string.IsNullOrWhiteSpace(item.AttributedPublishedContentRecordId) &&
            attributedRecordIds.Contains(item.AttributedPublishedContentRecordId));

        var topPerformer = ResolveTopPerformer(source, latestMetricSnapshots);
        var leadsGenerated = source.LeadProfiles.Count;
        var marketingQualifiedLeads = source.LeadProfiles.Count(item => item.CurrentStage >= LeadLifecycleStage.MarketingQualified);
        var bookingReadyLeads = source.LeadProfiles.Count(item => item.CurrentStage >= LeadLifecycleStage.BookingReady);
        var appointmentsBooked = source.BookingRecords.Count(item => item.Status == Domain.Booking.BookingStatus.Booked);
        var reminderTouchesScheduled = source.ReminderSchedules.Sum(item => item.Touches.Count);
        var followUpTouchesScheduled = source.FollowUpSequences.Sum(item => item.Steps.Count);
        var bookingFocused = IsBookingFocused(tenant.Profile.DesiredAction);
        var bilingual = string.Equals(tenant.Profile.ContentLanguage, "Bilingual", StringComparison.OrdinalIgnoreCase);

        var estimatedEngagement = latestMetricSnapshots.Count > 0
            ? totalLikes + totalComments + totalShares
            : Math.Round(
                (averageQualityScore * Math.Max(postsPublished, 1)) +
                (marketingQualifiedLeads * 3.5) +
                (bilingual ? 2.0 : 0),
                2);
        var estimatedClicks = latestMetricSnapshots.Count > 0
            ? totalClicks
            : Math.Round(
                (appointmentsBooked * (bookingFocused ? 5.25 : 4.0)) +
                (marketingQualifiedLeads * (bookingFocused ? 1.75 : 1.25)),
                2);

        var snapshot = new MonthlyPerformanceSnapshot(
            _idGenerator.NewId("monthly_snapshot"),
            tenant.TenantId,
            monthKey,
            tenant.Profile.ContentLanguage,
            tenant.Profile.DesiredAction,
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
            _clock.UtcNow,
            totalReach,
            totalClicks,
            totalLikes,
            totalComments,
            totalShares,
            attributedLeads,
            attributedBookings);

        await _monthlyPerformanceSnapshotRepository.SaveAsync(snapshot, cancellationToken);

        return Result<GenerateMonthlyPerformanceSnapshotResponse>.Success(
            new GenerateMonthlyPerformanceSnapshotResponse(
                snapshot.MonthlyPerformanceSnapshotId,
                snapshot.MonthKey,
                snapshot.ContentLanguage,
                snapshot.PrimaryConversionAction,
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
                snapshot.EstimatedClicks,
                snapshot.TotalReach,
                snapshot.TotalClicks,
                snapshot.TotalLikes,
                snapshot.TotalComments,
                snapshot.TotalShares,
                snapshot.AttributedLeads,
                snapshot.AttributedBookings));
    }

    private static IReadOnlyList<Domain.Publishing.PublishedContentMetricSnapshot> SelectLatestMetricSnapshots(MonthlyPerformanceSource source) =>
        (source.PublishedContentMetricSnapshots ?? Array.Empty<Domain.Publishing.PublishedContentMetricSnapshot>())
            .GroupBy(item => item.PublishedContentRecordId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.CapturedUtc).First())
            .ToArray();

    private static string ResolveTopPerformer(
        MonthlyPerformanceSource source,
        IReadOnlyList<Domain.Publishing.PublishedContentMetricSnapshot> latestMetricSnapshots)
    {
        if (source.PrimaryAssets.Count == 0)
        {
            return "No content generated";
        }

        if (latestMetricSnapshots.Count > 0)
        {
            var scoreByRequestId = source.PublishedContentRecords
                .Join(
                    latestMetricSnapshots,
                    record => record.PublishedContentRecordId,
                    snapshot => snapshot.PublishedContentRecordId,
                    (record, snapshot) => new
                    {
                        record.DailyContentRequestId,
                        Score = snapshot.Likes + snapshot.Comments + snapshot.Shares + snapshot.Clicks
                    })
                .GroupBy(item => item.DailyContentRequestId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => item.Score),
                    StringComparer.Ordinal);

            var topByMetrics = source.PrimaryAssets
                .OrderByDescending(asset => scoreByRequestId.TryGetValue(asset.DailyContentRequestId, out var score) ? score : 0)
                .ThenBy(asset => asset.Headline, StringComparer.Ordinal)
                .Select(asset => asset.Headline)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(topByMetrics))
            {
                return topByMetrics;
            }
        }

        var qualityScoreByRequestId = source.QualityReviews.ToDictionary(item => item.DailyContentRequestId, item => item.OverallScore, StringComparer.Ordinal);

        return source.PrimaryAssets
            .OrderByDescending(asset => qualityScoreByRequestId.TryGetValue(asset.DailyContentRequestId, out var score) ? score : 0)
            .ThenBy(asset => asset.Headline, StringComparer.Ordinal)
            .Select(asset => asset.Headline)
            .First();
    }

    private static bool IsBookingFocused(string? desiredAction) =>
        !string.IsNullOrWhiteSpace(desiredAction) &&
        (desiredAction.Contains("book", StringComparison.OrdinalIgnoreCase) ||
         desiredAction.Contains("consult", StringComparison.OrdinalIgnoreCase) ||
         desiredAction.Contains("appointment", StringComparison.OrdinalIgnoreCase) ||
         desiredAction.Contains("call", StringComparison.OrdinalIgnoreCase));
}
