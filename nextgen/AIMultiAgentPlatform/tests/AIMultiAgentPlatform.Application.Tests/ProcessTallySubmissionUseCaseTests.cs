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

    [Fact]
    public async Task ExecuteAsync_WithRealisticTallyPayload_DerivesPublishingPlatformsAndCalendly()
    {
        var tenantRepository = new FakeTenantRepository();
        var useCase = new ProcessTallySubmissionUseCase(
            tenantRepository,
            new FakeStrategyPlanRepository(),
            new FakeEditorialBacklogRepository(),
            new DeterministicIdGenerator(),
            new FixedClock());

        var request = new TallySubmissionRequest(
            "sub_124",
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "B2B consultants",
            Platforms:
            [
                "https://www.instagram.com/rnmgrowth",
                "https://www.linkedin.com/company/rnm-growth",
                "https://calendly.com/rnm-growth/intro-call"
            ],
            PrimaryContactPhone: "+1 555 010 9999");

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(tenantRepository.Saved);
        Assert.Equal("+1 555 010 9999", tenantRepository.Saved!.Profile.PrimaryContactPhone);
        Assert.Equal("https://calendly.com/rnm-growth/intro-call", tenantRepository.Saved.Profile.CalendlyUrl);
        Assert.Contains("Instagram", tenantRepository.Saved.Profile.Platforms);
        Assert.Contains("LinkedIn", tenantRepository.Saved.Profile.Platforms);
        Assert.DoesNotContain("Calendly", tenantRepository.Saved.Profile.Platforms);
        Assert.Equal(3, tenantRepository.Saved.Profile.PlatformLinks!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WithImageBasedTallyPayload_PreservesBusinessContext()
    {
        var tenantRepository = new FakeTenantRepository();
        var useCase = new ProcessTallySubmissionUseCase(
            tenantRepository,
            new FakeStrategyPlanRepository(),
            new FakeEditorialBacklogRepository(),
            new DeterministicIdGenerator(),
            new FixedClock());

        var request = new TallySubmissionRequest(
            "sub_125",
            "Acme Tax & Insurance",
            "Maria Rivera",
            "maria@acme.test",
            "Tax and insurance services",
            PrimaryContactPhone: "+1 713 555 0142",
            MainGoal: "Generate more booked consultations from social media content",
            MainOffer: "Small business tax planning",
            IdealClientDescription: "Small business owners who want to reduce tax surprises and stay compliant.",
            DesiredAction: "Book a consultation from the content",
            BrandTonePreference: "Professional",
            ContentLanguage: "Bilingual",
            InstagramUrl: "https://www.instagram.com/acmetax",
            FacebookPageUrl: "https://www.facebook.com/acmetax",
            LinkedInUrl: "https://www.linkedin.com/company/acmetax",
            CalendlyUrl: "https://calendly.com/acmetax/consultation",
            PainPointsText: "Unexpected tax bills\nConfusing compliance deadlines\nLack of a proactive tax strategy",
            ObjectionsText: "I already have an accountant\nIt sounds expensive\nI do not have time right now",
            AvoidTopicsText: "Guarantees\nPolitical opinions");

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(tenantRepository.Saved);
        Assert.Equal("Generate more booked consultations from social media content", tenantRepository.Saved!.Profile.MainGoal);
        Assert.Equal("Small business tax planning", tenantRepository.Saved.Profile.Offer);
        Assert.Equal("Small business owners who want to reduce tax surprises and stay compliant.", tenantRepository.Saved.Profile.TargetAudience);
        Assert.Equal("Book a consultation from the content", tenantRepository.Saved.Profile.DesiredAction);
        Assert.Equal("Bilingual", tenantRepository.Saved.Profile.ContentLanguage);
        Assert.Equal("BOOK", tenantRepository.Saved.Profile.CallToActionKeyword);
        Assert.Equal("https://calendly.com/acmetax/consultation", tenantRepository.Saved.Profile.CalendlyUrl);
        Assert.Equal(3, tenantRepository.Saved.Profile.PainPoints.Count);
        Assert.Equal(2, tenantRepository.Saved.Profile.AvoidTopics.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailureWhenLanguageIsUnsupported()
    {
        var useCase = new ProcessTallySubmissionUseCase(
            new FakeTenantRepository(),
            new FakeStrategyPlanRepository(),
            new FakeEditorialBacklogRepository(),
            new DeterministicIdGenerator(),
            new FixedClock());

        var request = new TallySubmissionRequest(
            "sub_126",
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "B2B consultants",
            ContentLanguage: "French");

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("intake.language.invalid", result.ErrorCode);
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
