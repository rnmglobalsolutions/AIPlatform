using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Intake;
using AIMultiAgentPlatform.Domain.Strategy;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class ProcessTallySubmissionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesTenantStrategyAndEditorialBacklog()
    {
        var tenantRepository = new FakeTenantRepository();
        var receiptRepository = new FakeTallySubmissionReceiptRepository();
        var strategyPlanRepository = new FakeStrategyPlanRepository();
        var backlogRepository = new FakeEditorialBacklogRepository();
        var useCase = new ProcessTallySubmissionUseCase(
            tenantRepository,
            receiptRepository,
            strategyPlanRepository,
            backlogRepository,
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
        Assert.StartsWith("tenant_", result.Value!.TenantId);
        Assert.Equal("rnm-growth", result.Value.Slug);
        Assert.StartsWith("strategy_", result.Value.StrategyPlanId);
        Assert.StartsWith("backlog_", result.Value.EditorialBacklogId);
        Assert.Equal(14, result.Value.BacklogItemCount);
        Assert.Equal(result.Value.TenantId, tenantRepository.Saved!.TenantId.Value);
        Assert.Equal("rnm-growth", tenantRepository.Saved.Slug);
        Assert.Equal(14, backlogRepository.Saved!.Items.Count);
        Assert.Contains(backlogRepository.Saved.Items, item => item.PrimaryFormat == PrimaryFormat.ShortVideo);
        Assert.Contains(backlogRepository.Saved.Items, item => item.LeadGoal == "comment_keyword");
        Assert.Contains(backlogRepository.Saved.Items, item => item.Category == ContentCategory.CtaDriven && item.UsesCallToActionKeyword);
        Assert.Equal(1, strategyPlanRepository.Saved!.DailyPostingCadenceDays);
        Assert.Equal(3, strategyPlanRepository.Saved.VideoCadenceDays);
        Assert.Equal(result.Value.TenantId, receiptRepository.Saved!.TenantId);
        Assert.Contains("Content-led growth strategy for Founders in B2B consultants", strategyPlanRepository.Saved.ContentPillars);
        Assert.Contains("How Founders can solve low visibility", strategyPlanRepository.Saved.ContentPillars);
        Assert.Contains("How Founders can solve weak pipeline", strategyPlanRepository.Saved.ContentPillars);
        Assert.Contains("How Founders can overcome no time", strategyPlanRepository.Saved.ContentPillars);
        Assert.DoesNotContain("Pain point education", strategyPlanRepository.Saved.ContentPillars);
        Assert.Contains("positions content-led growth as the trusted answer to low visibility", strategyPlanRepository.Saved.StrategicNarrative, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailureWhenBusinessNameMissing()
    {
        var useCase = new ProcessTallySubmissionUseCase(
            new FakeTenantRepository(),
            new FakeTallySubmissionReceiptRepository(),
            new FakeStrategyPlanRepository(),
            new FakeEditorialBacklogRepository(),
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
            new FakeTallySubmissionReceiptRepository(),
            new FakeStrategyPlanRepository(),
            new FakeEditorialBacklogRepository(),
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
            WebsiteUrl: "https://rnmgrowth.com",
            PrimaryContactPhone: "+1 555 010 9999");

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(tenantRepository.Saved);
        Assert.Equal("+1 555 010 9999", tenantRepository.Saved!.Profile.PrimaryContactPhone);
        Assert.Equal("https://calendly.com/rnm-growth/intro-call", tenantRepository.Saved.Profile.CalendlyUrl);
        Assert.Equal("https://rnmgrowth.com", tenantRepository.Saved.Profile.WebsiteUrl);
        Assert.Contains("Instagram", tenantRepository.Saved.Profile.Platforms);
        Assert.Contains("LinkedIn", tenantRepository.Saved.Profile.Platforms);
        Assert.DoesNotContain("Calendly", tenantRepository.Saved.Profile.Platforms);
        Assert.Equal(3, tenantRepository.Saved.Profile.PlatformLinks!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WithImageBasedTallyPayload_PreservesBusinessContext()
    {
        var tenantRepository = new FakeTenantRepository();
        var strategyPlanRepository = new FakeStrategyPlanRepository();
        var useCase = new ProcessTallySubmissionUseCase(
            tenantRepository,
            new FakeTallySubmissionReceiptRepository(),
            strategyPlanRepository,
            new FakeEditorialBacklogRepository(),
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
            WebsiteUrl: "https://acmetax.com",
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
        Assert.Equal("https://acmetax.com", tenantRepository.Saved.Profile.WebsiteUrl);
        Assert.Equal(3, tenantRepository.Saved.Profile.PainPoints.Count);
        Assert.Equal(2, tenantRepository.Saved.Profile.AvoidTopics.Count);
        Assert.Contains("Small business tax planning strategy for Small business owners who want to reduce tax surprises and stay compliant in Tax and insurance services", strategyPlanRepository.Saved!.ContentPillars);
        Assert.Contains("How Small business owners who want to reduce tax surprises and stay compliant can solve unexpected tax bills", strategyPlanRepository.Saved.ContentPillars);
        Assert.Contains("How Small business owners who want to reduce tax surprises and stay compliant can solve confusing compliance deadlines", strategyPlanRepository.Saved.ContentPillars);
        Assert.Contains("How Small business owners who want to reduce tax surprises and stay compliant can overcome i already have an accountant", strategyPlanRepository.Saved.ContentPillars);
        Assert.Contains("Conversion content that moves Small business owners who want to reduce tax surprises and stay compliant toward booked consultations", strategyPlanRepository.Saved.ContentPillars);
        Assert.Contains("moves the audience toward booking a consultation through the scheduling link", strategyPlanRepository.Saved.StrategicNarrative, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailureWhenWebsiteUrlIsInvalid()
    {
        var useCase = new ProcessTallySubmissionUseCase(
            new FakeTenantRepository(),
            new FakeTallySubmissionReceiptRepository(),
            new FakeStrategyPlanRepository(),
            new FakeEditorialBacklogRepository(),
            new FixedClock());

        var request = new TallySubmissionRequest(
            "sub_127",
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "B2B consultants",
            WebsiteUrl: "ftp://rnmgrowth.com");

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("intake.website-url.invalid", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailureWhenLanguageIsUnsupported()
    {
        var useCase = new ProcessTallySubmissionUseCase(
            new FakeTenantRepository(),
            new FakeTallySubmissionReceiptRepository(),
            new FakeStrategyPlanRepository(),
            new FakeEditorialBacklogRepository(),
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

    [Fact]
    public async Task ExecuteAsync_WhenSubmissionIsRetried_ReturnsExistingResponseWithoutCreatingNewArtifacts()
    {
        var tenantRepository = new FakeTenantRepository();
        var receiptRepository = new FakeTallySubmissionReceiptRepository();
        var strategyPlanRepository = new FakeStrategyPlanRepository();
        var backlogRepository = new FakeEditorialBacklogRepository();
        var useCase = new ProcessTallySubmissionUseCase(
            tenantRepository,
            receiptRepository,
            strategyPlanRepository,
            backlogRepository,
            new FixedClock());

        var request = new TallySubmissionRequest(
            "sub_retry_001",
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "B2B consultants",
            Platforms: ["https://www.instagram.com/rnmgrowth"]);

        var first = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);
        var second = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(1, tenantRepository.SaveCount);
        Assert.Equal(1, strategyPlanRepository.SaveCount);
        Assert.Equal(1, backlogRepository.SaveCount);
        Assert.Equal(1, receiptRepository.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDesiredActionIsWebsiteVisit_BacklogLeadGoalMatchesWebsiteTraffic()
    {
        var strategyPlanRepository = new FakeStrategyPlanRepository();
        var backlogRepository = new FakeEditorialBacklogRepository();
        var useCase = new ProcessTallySubmissionUseCase(
            new FakeTenantRepository(),
            new FakeTallySubmissionReceiptRepository(),
            strategyPlanRepository,
            backlogRepository,
            new FixedClock());

        var request = new TallySubmissionRequest(
            "sub_website_001",
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "Marketing consulting",
            MainGoal: "Drive more qualified traffic to the website",
            MainOffer: "Conversion-focused content systems",
            IdealClientDescription: "Service businesses with inconsistent lead flow",
            DesiredAction: "Visit the website and request more information",
            WebsiteUrl: "https://rnmgrowth.com",
            PainPointsText: "Low visibility\nWeak conversion path",
            ObjectionsText: "We already post consistently");

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(backlogRepository.Saved);
        Assert.All(backlogRepository.Saved!.Items, item => Assert.Equal("visit_website", item.LeadGoal));
        Assert.Contains(backlogRepository.Saved.Items, item => item.Category == ContentCategory.CtaDriven && item.UsesCallToActionKeyword is false);
        Assert.Contains("toward high-intent website visits", string.Join(" | ", strategyPlanRepository.Saved!.ContentPillars), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDesiredActionIsCommentKeyword_BacklogKeepsKeywordDrivenCallsToAction()
    {
        var backlogRepository = new FakeEditorialBacklogRepository();
        var useCase = new ProcessTallySubmissionUseCase(
            new FakeTenantRepository(),
            new FakeTallySubmissionReceiptRepository(),
            new FakeStrategyPlanRepository(),
            backlogRepository,
            new FixedClock());

        var request = new TallySubmissionRequest(
            "sub_comment_001",
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "Marketing consulting",
            MainGoal: "Start more inbound conversations",
            MainOffer: "Lead-gen content systems",
            IdealClientDescription: "Founders with inconsistent outreach",
            DesiredAction: "Comment BOOK to get the next step",
            CallToActionKeyword: "BOOK",
            PainPointsText: "Inconsistent demand",
            ObjectionsText: "I do not want to be pushy");

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(backlogRepository.Saved);
        Assert.Contains(backlogRepository.Saved!.Items, item => item.LeadGoal == "comment_keyword");
        Assert.Contains(backlogRepository.Saved.Items, item => item.Category == ContentCategory.CtaDriven && item.UsesCallToActionKeyword);
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public Tenant? Saved { get; private set; }
        public int SaveCount { get; private set; }

        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            SaveCount++;
            Saved = tenant;
            return Task.CompletedTask;
        }

        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.TenantId.Value == tenantId ? Saved : null);
    }

    private sealed class FakeStrategyPlanRepository : IStrategyPlanRepository
    {
        public StrategyPlan? Saved { get; private set; }
        public int SaveCount { get; private set; }

        public Task SaveAsync(StrategyPlan strategyPlan, CancellationToken cancellationToken)
        {
            SaveCount++;
            Saved = strategyPlan;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEditorialBacklogRepository : IEditorialBacklogRepository
    {
        public EditorialBacklog? Saved { get; private set; }
        public int SaveCount { get; private set; }

        public Task SaveAsync(EditorialBacklog backlog, CancellationToken cancellationToken)
        {
            SaveCount++;
            Saved = backlog;
            return Task.CompletedTask;
        }

        public Task<EditorialBacklog?> FindByIdAsync(string backlogId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.EditorialBacklogId == backlogId ? Saved : null);
    }

    private sealed class FakeTallySubmissionReceiptRepository : ITallySubmissionReceiptRepository
    {
        private readonly Dictionary<string, TallySubmissionReceipt> _items = new(StringComparer.OrdinalIgnoreCase);
        public TallySubmissionReceipt? Saved { get; private set; }
        public int SaveCount { get; private set; }

        public Task SaveAsync(TallySubmissionReceipt receipt, CancellationToken cancellationToken)
        {
            SaveCount++;
            Saved = receipt;
            _items[receipt.ExternalSubmissionId] = receipt;
            return Task.CompletedTask;
        }

        public Task<TallySubmissionReceipt?> FindByExternalSubmissionIdAsync(string externalSubmissionId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.TryGetValue(externalSubmissionId.Trim(), out var receipt) ? receipt : null);
    }
}
