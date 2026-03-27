using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Reporting;

namespace AIMultiAgentPlatform.Infrastructure.Reporting;

public sealed class InMemoryMonthlyPerformanceReadService : IMonthlyPerformanceReadService
{
    private readonly Persistence.InMemoryDailyContentRequestRepository _dailyContentRequestRepository;
    private readonly Persistence.InMemoryPrimaryAssetRepository _primaryAssetRepository;
    private readonly Persistence.InMemoryQualityReviewRepository _qualityReviewRepository;
    private readonly Persistence.InMemoryApprovalRequestRepository _approvalRequestRepository;
    private readonly Persistence.InMemorySchedulingJobRepository _schedulingJobRepository;
    private readonly Persistence.InMemoryPublishedContentRecordRepository _publishedContentRecordRepository;
    private readonly Persistence.InMemoryLeadProfileRepository _leadProfileRepository;
    private readonly Persistence.InMemoryBookingRecordRepository _bookingRecordRepository;
    private readonly Persistence.InMemoryReminderScheduleRepository _reminderScheduleRepository;
    private readonly Persistence.InMemoryFollowUpSequenceRepository _followUpSequenceRepository;

    public InMemoryMonthlyPerformanceReadService(
        Persistence.InMemoryDailyContentRequestRepository dailyContentRequestRepository,
        Persistence.InMemoryPrimaryAssetRepository primaryAssetRepository,
        Persistence.InMemoryQualityReviewRepository qualityReviewRepository,
        Persistence.InMemoryApprovalRequestRepository approvalRequestRepository,
        Persistence.InMemorySchedulingJobRepository schedulingJobRepository,
        Persistence.InMemoryPublishedContentRecordRepository publishedContentRecordRepository,
        Persistence.InMemoryLeadProfileRepository leadProfileRepository,
        Persistence.InMemoryBookingRecordRepository bookingRecordRepository,
        Persistence.InMemoryReminderScheduleRepository reminderScheduleRepository,
        Persistence.InMemoryFollowUpSequenceRepository followUpSequenceRepository)
    {
        _dailyContentRequestRepository = dailyContentRequestRepository;
        _primaryAssetRepository = primaryAssetRepository;
        _qualityReviewRepository = qualityReviewRepository;
        _approvalRequestRepository = approvalRequestRepository;
        _schedulingJobRepository = schedulingJobRepository;
        _publishedContentRecordRepository = publishedContentRecordRepository;
        _leadProfileRepository = leadProfileRepository;
        _bookingRecordRepository = bookingRecordRepository;
        _reminderScheduleRepository = reminderScheduleRepository;
        _followUpSequenceRepository = followUpSequenceRepository;
    }

    public Task<MonthlyPerformanceSource> ReadAsync(string tenantId, int year, int month, CancellationToken cancellationToken)
    {
        var requestIds = _dailyContentRequestRepository
            .ListAll()
            .Where(item => item.TenantId.Value == tenantId && item.RequestedUtc.Year == year && item.RequestedUtc.Month == month)
            .Select(item => item.DailyContentRequestId)
            .ToHashSet(StringComparer.Ordinal);

        var leadProfiles = _leadProfileRepository
            .ListAll()
            .Where(item => item.TenantId.Value == tenantId && item.UpdatedUtc.Year == year && item.UpdatedUtc.Month == month)
            .ToArray();

        var bookingRecords = _bookingRecordRepository
            .ListAll()
            .Where(item => item.TenantId.Value == tenantId && item.UpdatedUtc.Year == year && item.UpdatedUtc.Month == month)
            .ToArray();

        var reminderSchedules = _reminderScheduleRepository
            .ListAll()
            .Where(item => item.TenantId.Value == tenantId && item.CreatedUtc.Year == year && item.CreatedUtc.Month == month)
            .ToArray();

        var followUpSequences = _followUpSequenceRepository
            .ListAll()
            .Where(item => item.TenantId.Value == tenantId && item.CreatedUtc.Year == year && item.CreatedUtc.Month == month)
            .ToArray();

        return Task.FromResult(
            new MonthlyPerformanceSource(
                _dailyContentRequestRepository.ListAll().Where(item => requestIds.Contains(item.DailyContentRequestId)).ToArray(),
                _primaryAssetRepository.ListAll().Where(item => requestIds.Contains(item.DailyContentRequestId)).ToArray(),
                _qualityReviewRepository.ListAll().Where(item => requestIds.Contains(item.DailyContentRequestId)).ToArray(),
                _approvalRequestRepository.ListAll().Where(item => requestIds.Contains(item.DailyContentRequestId)).ToArray(),
                _schedulingJobRepository.ListAll().Where(item => requestIds.Contains(item.DailyContentRequestId)).ToArray(),
                _publishedContentRecordRepository.ListAll().Where(item => requestIds.Contains(item.DailyContentRequestId)).ToArray(),
                leadProfiles,
                bookingRecords,
                reminderSchedules,
                followUpSequences));
    }
}
