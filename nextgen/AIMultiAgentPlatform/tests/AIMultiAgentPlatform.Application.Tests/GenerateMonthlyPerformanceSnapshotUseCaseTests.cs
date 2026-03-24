using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Contracts.Reporting;
using AIMultiAgentPlatform.Domain.Booking;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.FollowUps;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Reminders;
using AIMultiAgentPlatform.Domain.Reporting;
using AIMultiAgentPlatform.Domain.Reviewing;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class GenerateMonthlyPerformanceSnapshotUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_AggregatesMonthlySnapshot()
    {
        var tenant = Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "Agencies",
                "AI content systems",
                "Founders",
                "Bold",
                "BOOK",
                ["Instagram", "LinkedIn"],
                ["Low engagement"],
                ["No time"],
                ["Politics"]),
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

        var source = new MonthlyPerformanceSource(
            [new DailyContentRequest("daily_request_001", tenant.TenantId, "backlog_001", 3, new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc), "corr-1")],
            [new PrimaryAsset("primary_asset_001", "daily_request_001", tenant.TenantId, PrimaryFormat.ShortVideo, "Short video: Authority", "Hook", "Body", "Payoff", "CTA", "Notes")],
            [new QualityReview("quality_001", "daily_request_001", tenant.TenantId, 8.5, 8.4, 8.8, 9.1, 8.7, "Feedback", "Optimized CTA", DateTime.UtcNow)],
            [new ApprovalRequest("approval_001", "daily_request_001", tenant.TenantId, ApprovalStatus.Approved, "Approved", DateTime.UtcNow)],
            [new SchedulingJob("schedule_001", "daily_request_001", tenant.TenantId, SchedulingStatus.Scheduled, "Scheduled", DateTime.UtcNow, [new PublicationTarget("Instagram", DateTime.UtcNow, "Payload"), new PublicationTarget("LinkedIn", DateTime.UtcNow, "Payload")])],
            [new LeadProfile("lead_001", tenant.TenantId, "contact_001", "Jane", "Doe", "jane@rnm.test", "Instagram", LeadLifecycleStage.Booked, "Intent", "BOOK", DateTime.UtcNow)],
            [new BookingRecord("booking_001", tenant.TenantId, "lead_001", "contact_001", BookingStatus.Booked, "https://calendly.test", "strategy-call", DateTime.UtcNow, DateTime.UtcNow)],
            [new ReminderSchedule("reminder_001", tenant.TenantId, "booking_001", ReminderScheduleStatus.Scheduled, [new ReminderTouch(Domain.Communications.CommunicationChannel.Email, DateTime.UtcNow, "appointment-reminder-24h")], DateTime.UtcNow)],
            [new FollowUpSequence("followup_001", tenant.TenantId, "lead_001", FollowUpSequenceStatus.Scheduled, "No booking", [new FollowUpStep(Domain.Communications.CommunicationChannel.Instagram, DateTime.UtcNow, "follow-up-day-1")], DateTime.UtcNow)]);

        var snapshotRepository = new FakeSnapshotRepository();
        var useCase = new GenerateMonthlyPerformanceSnapshotUseCase(
            new FakeTenantRepository(tenant),
            new FakeReadService(source),
            snapshotRepository,
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new GenerateMonthlyPerformanceSnapshotCommand(
                new GenerateMonthlyPerformanceSnapshotRequest(tenant.TenantId.Value, 2026, 3, "corr-report"),
                "corr-report"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("monthly_snapshot_001", result.Value!.MonthlyPerformanceSnapshotId);
        Assert.Equal(2, result.Value.PostsPublished);
        Assert.Equal(1, result.Value.VideosCreated);
        Assert.Equal(0, result.Value.GraphicsCreated);
        Assert.Equal("Short video: Authority", result.Value.TopPerformingAssetTitle);
        Assert.Equal(8.7, result.Value.AverageQualityScore);
        Assert.Equal(1, result.Value.AppointmentsBooked);
        Assert.NotNull(snapshotRepository.Saved);
    }

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(tenant.TenantId.Value == tenantId ? tenant : null);
    }

    private sealed class FakeReadService(MonthlyPerformanceSource source) : IMonthlyPerformanceReadService
    {
        public Task<MonthlyPerformanceSource> ReadAsync(string tenantId, int year, int month, CancellationToken cancellationToken) =>
            Task.FromResult(source);
    }

    private sealed class FakeSnapshotRepository : IMonthlyPerformanceSnapshotRepository
    {
        public MonthlyPerformanceSnapshot? Saved { get; private set; }
        public Task SaveAsync(MonthlyPerformanceSnapshot snapshot, CancellationToken cancellationToken)
        {
            Saved = snapshot;
            return Task.CompletedTask;
        }
        public Task<MonthlyPerformanceSnapshot?> FindAsync(string tenantId, string monthKey, CancellationToken cancellationToken) =>
            Task.FromResult(Saved is not null && Saved.TenantId.Value == tenantId && Saved.MonthKey == monthKey ? Saved : null);
    }

    private sealed class DeterministicIdGenerator : IIdGenerator
    {
        private int _sequence;
        public string NewId(string prefix)
        {
            _sequence++;
            return $"{prefix}_{_sequence:000}";
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc);
    }
}
