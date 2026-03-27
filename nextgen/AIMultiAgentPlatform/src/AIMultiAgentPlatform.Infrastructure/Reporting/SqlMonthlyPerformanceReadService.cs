using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Domain.Booking;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.FollowUps;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Reminders;
using AIMultiAgentPlatform.Domain.Reviewing;
using AIMultiAgentPlatform.Infrastructure.Persistence;
using AIMultiAgentPlatform.Infrastructure.Persistence.Sql;
using AIMultiAgentPlatform.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIMultiAgentPlatform.Infrastructure.Reporting;

public sealed class SqlMonthlyPerformanceReadService : IMonthlyPerformanceReadService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<AiPlatformDbContext> _dbContextFactory;
    private readonly InMemoryQualityReviewRepository _qualityReviewRepository;
    private readonly InMemoryApprovalRequestRepository _approvalRequestRepository;

    public SqlMonthlyPerformanceReadService(
        IDbContextFactory<AiPlatformDbContext> dbContextFactory,
        InMemoryQualityReviewRepository qualityReviewRepository,
        InMemoryApprovalRequestRepository approvalRequestRepository)
    {
        _dbContextFactory = dbContextFactory;
        _qualityReviewRepository = qualityReviewRepository;
        _approvalRequestRepository = approvalRequestRepository;
    }

    public async Task<MonthlyPerformanceSource> ReadAsync(string tenantId, int year, int month, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var monthStartUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndUtc = monthStartUtc.AddMonths(1);

        var dailyRequests = await LoadAsync<DailyContentRequest>(
            dbContext,
            "DailyContentRequest",
            query => query.Where(item =>
                item.TenantId == tenantId &&
                item.SortUtc >= monthStartUtc &&
                item.SortUtc < monthEndUtc),
            cancellationToken);

        var requestIds = dailyRequests
            .Select(item => item.DailyContentRequestId)
            .ToHashSet(StringComparer.Ordinal);

        var primaryAssets = await LoadAsync<PrimaryAsset>(
            dbContext,
            "PrimaryAsset",
            query => query.Where(item => requestIds.Contains(item.LookupKey)),
            cancellationToken);

        var schedulingJobs = await LoadAsync<SchedulingJob>(
            dbContext,
            "SchedulingJob",
            query => query.Where(item => requestIds.Contains(item.LookupKey)),
            cancellationToken);

        var publishedContentRecords = await LoadAsync<PublishedContentRecord>(
            dbContext,
            "PublishedContentRecord",
            query => query.Where(item => requestIds.Contains(item.LookupKey)),
            cancellationToken);

        var publishedContentRecordIds = publishedContentRecords
            .Select(item => item.PublishedContentRecordId)
            .ToHashSet(StringComparer.Ordinal);

        var publishedContentMetricSnapshots = publishedContentRecordIds.Count == 0
            ? Array.Empty<PublishedContentMetricSnapshot>()
            : await LoadAsync<PublishedContentMetricSnapshot>(
                dbContext,
                "PublishedContentMetricSnapshot",
                query => query.Where(item =>
                    publishedContentRecordIds.Contains(item.LookupKey) &&
                    item.SortUtc >= monthStartUtc &&
                    item.SortUtc < monthEndUtc),
                cancellationToken);

        var leadProfiles = await LoadAsync<LeadProfile>(
            dbContext,
            "LeadProfile",
            query => query.Where(item =>
                item.TenantId == tenantId &&
                item.SortUtc >= monthStartUtc &&
                item.SortUtc < monthEndUtc),
            cancellationToken);

        var bookingRecords = await LoadAsync<BookingRecord>(
            dbContext,
            "BookingRecord",
            query => query.Where(item =>
                item.TenantId == tenantId &&
                item.SortUtc >= monthStartUtc &&
                item.SortUtc < monthEndUtc),
            cancellationToken);

        var reminderSchedules = await LoadAsync<ReminderSchedule>(
            dbContext,
            "ReminderSchedule",
            query => query.Where(item =>
                item.TenantId == tenantId &&
                item.SortUtc >= monthStartUtc &&
                item.SortUtc < monthEndUtc),
            cancellationToken);

        var followUpSequences = await LoadAsync<FollowUpSequence>(
            dbContext,
            "FollowUpSequence",
            query => query.Where(item =>
                item.TenantId == tenantId &&
                item.SortUtc >= monthStartUtc &&
                item.SortUtc < monthEndUtc),
            cancellationToken);

        var qualityReviews = await LoadAsync<QualityReview>(
            dbContext,
            "QualityReview",
            query => query.Where(item => requestIds.Contains(item.LookupKey)),
            cancellationToken);

        var approvalRequests = await LoadAsync<ApprovalRequest>(
            dbContext,
            "ApprovalRequest",
            query => query.Where(item => requestIds.Contains(item.LookupKey)),
            cancellationToken);

        var normalizedQualityReviews = qualityReviews.Count > 0
            ? qualityReviews
            : _qualityReviewRepository
                .ListAll()
                .Where(item => requestIds.Contains(item.DailyContentRequestId))
                .ToArray();

        var normalizedApprovalRequests = approvalRequests.Count > 0
            ? approvalRequests
            : _approvalRequestRepository
                .ListAll()
                .Where(item => requestIds.Contains(item.DailyContentRequestId))
                .ToArray();

        return new MonthlyPerformanceSource(
            dailyRequests,
            primaryAssets,
            normalizedQualityReviews,
            normalizedApprovalRequests,
            schedulingJobs,
            publishedContentRecords,
            leadProfiles,
            bookingRecords,
            reminderSchedules,
            followUpSequences,
            publishedContentMetricSnapshots);
    }

    private static async Task<IReadOnlyList<T>> LoadAsync<T>(
        AiPlatformDbContext dbContext,
        string aggregateType,
        Func<IQueryable<SqlAggregateDocumentEntity>, IQueryable<SqlAggregateDocumentEntity>> buildQuery,
        CancellationToken cancellationToken)
    {
        var entities = await buildQuery(
                dbContext.AggregateDocuments.AsNoTracking().Where(item => item.AggregateType == aggregateType))
            .ToListAsync(cancellationToken);

        return entities
            .Select(static entity => JsonSerializer.Deserialize<T>(entity.PayloadJson, SerializerOptions))
            .Where(static item => item is not null)
            .Cast<T>()
            .ToArray();
    }
}
