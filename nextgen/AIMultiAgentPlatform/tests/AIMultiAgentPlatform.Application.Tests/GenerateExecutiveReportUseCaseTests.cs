using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Contracts.Reporting;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Reporting;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class GenerateExecutiveReportUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsExecutiveSummaryAndRecommendations()
    {
        var tenant = Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "Agencies",
                "Growth systems",
                "Founders",
                "Bold",
                "BOOK",
                ["Instagram"],
                ["Low visibility"],
                ["No time"],
                Array.Empty<string>()),
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

        var snapshot = new MonthlyPerformanceSnapshot(
            "monthly_snapshot_001",
            tenant.TenantId,
            "2026-03",
            "English",
            "Book a call",
            8,
            4,
            1,
            "Short video: Authority",
            8.8,
            7,
            1,
            5,
            4,
            3,
            2,
            4,
            3,
            21,
            14,
            DateTime.UtcNow,
            5400,
            14,
            10,
            7,
            4,
            3,
            2);

        var source = new MonthlyPerformanceSource(
            Array.Empty<Domain.Content.DailyContentRequest>(),
            Array.Empty<Domain.Content.PrimaryAsset>(),
            Array.Empty<Domain.Reviewing.QualityReview>(),
            Array.Empty<Domain.Reviewing.ApprovalRequest>(),
            Array.Empty<Domain.Publishing.SchedulingJob>(),
            Array.Empty<Domain.Publishing.PublishedContentRecord>(),
            Array.Empty<Domain.Leads.LeadProfile>(),
            Array.Empty<Domain.Booking.BookingRecord>(),
            Array.Empty<Domain.Reminders.ReminderSchedule>(),
            Array.Empty<Domain.FollowUps.FollowUpSequence>());

        var useCase = new GenerateExecutiveReportUseCase(
            new FakeTenantRepository(tenant),
            new FakeReadService(source),
            new FakeSnapshotRepository(snapshot),
            new GenerateMonthlyPerformanceSnapshotUseCase(
                new NoopTenantRepository(),
                new NoopReadService(),
                new NoopSnapshotRepository(),
                new NoopIdGenerator(),
                new NoopClock()),
            new FakeReportAgent());

        var result = await useCase.ExecuteAsync(
            new GenerateExecutiveReportRequest(tenant.TenantId.Value, 2026, 3, "corr-exec"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("monthly_snapshot_001", result.Value!.MonthlyPerformanceSnapshotId);
        Assert.Equal("Executive summary", result.Value.ExecutiveSummary);
        Assert.Single(result.Value.Recommendations);
        Assert.Equal("Double Down", result.Value.Recommendations[0].Title);
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

    private sealed class FakeSnapshotRepository(MonthlyPerformanceSnapshot snapshot) : IMonthlyPerformanceSnapshotRepository
    {
        public Task SaveAsync(MonthlyPerformanceSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MonthlyPerformanceSnapshot?> FindAsync(string tenantId, string monthKey, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot.TenantId.Value == tenantId && snapshot.MonthKey == monthKey ? snapshot : null);
    }

    private sealed class FakeReportAgent : IReportAgent
    {
        public Task<ReportAgentResult> GenerateAsync(ReportGenerationContext context, CancellationToken cancellationToken) =>
            Task.FromResult(
                new ReportAgentResult(
                    "Executive summary",
                    "Operational summary",
                    [new ReportRecommendation("Double Down", "Medium", "Instagram won.", "Post more there.", "Top platform: Instagram")]));
    }

    private sealed class NoopTenantRepository : ITenantRepository
    {
        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult<Tenant?>(null);
    }

    private sealed class NoopReadService : IMonthlyPerformanceReadService
    {
        public Task<MonthlyPerformanceSource> ReadAsync(string tenantId, int year, int month, CancellationToken cancellationToken) =>
            Task.FromResult(new MonthlyPerformanceSource([], [], [], [], [], [], [], [], [], []));
    }

    private sealed class NoopSnapshotRepository : IMonthlyPerformanceSnapshotRepository
    {
        public Task SaveAsync(MonthlyPerformanceSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MonthlyPerformanceSnapshot?> FindAsync(string tenantId, string monthKey, CancellationToken cancellationToken) => Task.FromResult<MonthlyPerformanceSnapshot?>(null);
    }

    private sealed class NoopIdGenerator : IIdGenerator
    {
        public string NewId(string prefix) => prefix;
    }

    private sealed class NoopClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
