using AIMultiAgentPlatform.Domain.Booking;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.FollowUps;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Reminders;
using AIMultiAgentPlatform.Domain.Reviewing;

namespace AIMultiAgentPlatform.Application.Reporting;

public sealed record MonthlyPerformanceSource(
    IReadOnlyList<DailyContentRequest> DailyContentRequests,
    IReadOnlyList<PrimaryAsset> PrimaryAssets,
    IReadOnlyList<QualityReview> QualityReviews,
    IReadOnlyList<ApprovalRequest> ApprovalRequests,
    IReadOnlyList<SchedulingJob> SchedulingJobs,
    IReadOnlyList<PublishedContentRecord> PublishedContentRecords,
    IReadOnlyList<LeadProfile> LeadProfiles,
    IReadOnlyList<BookingRecord> BookingRecords,
    IReadOnlyList<ReminderSchedule> ReminderSchedules,
    IReadOnlyList<FollowUpSequence> FollowUpSequences,
    IReadOnlyList<PublishedContentMetricSnapshot>? PublishedContentMetricSnapshots = null);
