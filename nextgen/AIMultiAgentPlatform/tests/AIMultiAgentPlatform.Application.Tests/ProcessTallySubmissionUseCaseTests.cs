using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Strategy;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class ProcessTallySubmissionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesTenantStrategyAndEditorialBacklog()
    {
        var tenantRepository = new FakeTenantRepository();
        var strategyPlanRepository = new FakeStrategyPlanRepository();
        var backlogRepository = new FakeEditorialBacklogRepository();
        var useCase = new ProcessTallySubmissionUseCase(
            tenantRepository,
            strategyPlanRepository,
            backlogRepository,
            new DeterministicIdGenerator(),
            new FixedClock());

        var request = new TallySubmissionRequest(
            "sub_123",
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "B2B consultants",
            "Content-led growth",
            "Founders",
            "Confident",
            "BOOK",
            ["Instagram", "LinkedIn", "TikTok"],
            ["Low visibility", "Weak pipeline"],
            ["No time", "Unsure what to post"],
            ["Politics"],
            14);

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("tenant_001", result.Value!.TenantId);
        Assert.Equal("rnm-growth", result.Value.Slug);
        Assert.Equal("strategy_002", result.Value.StrategyPlanId);
        Assert.Equal("backlog_003", result.Value.EditorialBacklogId);
        Assert.Equal(14, result.Value.BacklogItemCount);
        Assert.Equal("rnm-growth", tenantRepository.Saved!.Slug);
        Assert.Equal(14, backlogRepository.Saved!.Items.Count);
        Assert.Contains(backlogRepository.Saved.Items, item => item.PrimaryFormat == PrimaryFormat.ShortVideo);
        Assert.Equal(1, strategyPlanRepository.Saved!.DailyPostingCadenceDays);
        Assert.Equal(3, strategyPlanRepository.Saved.VideoCadenceDays);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailureWhenBusinessNameMissing()
    {
        var useCase = new ProcessTallySubmissionUseCase(
            new FakeTenantRepository(),
            new FakeStrategyPlanRepository(),
            new FakeEditorialBacklogRepository(),
            new DeterministicIdGenerator(),
            new FixedClock());

        var request = new TallySubmissionRequest(
            "sub_123",
            "",
            "Jane Doe",
            "jane@rnm.test",
            "B2B consultants",
            "Content-led growth",
            "Founders",
            "Confident",
            "BOOK",
            ["Instagram"],
            ["Low visibility"],
            ["No time"],
            [],
            14);

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("intake.business-name.required", result.ErrorCode);
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

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public Tenant? Saved { get; private set; }

        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            Saved = tenant;
            return Task.CompletedTask;
        }

        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.TenantId.Value == tenantId ? Saved : null);
    }

    private sealed class FakeStrategyPlanRepository : IStrategyPlanRepository
    {
        public StrategyPlan? Saved { get; private set; }

        public Task SaveAsync(StrategyPlan strategyPlan, CancellationToken cancellationToken)
        {
            Saved = strategyPlan;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEditorialBacklogRepository : IEditorialBacklogRepository
    {
        public EditorialBacklog? Saved { get; private set; }

        public Task SaveAsync(EditorialBacklog backlog, CancellationToken cancellationToken)
        {
            Saved = backlog;
            return Task.CompletedTask;
        }

        public Task<EditorialBacklog?> FindByIdAsync(string backlogId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.EditorialBacklogId == backlogId ? Saved : null);
    }
}
