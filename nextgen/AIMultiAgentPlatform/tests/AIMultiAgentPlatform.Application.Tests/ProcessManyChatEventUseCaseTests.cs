using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.LeadGeneration;
using AIMultiAgentPlatform.Contracts.ManyChat;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class ProcessManyChatEventUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UsesKeywordToPromoteLeadToMarketingQualified()
    {
        var tenant = CreateTenant();
        var leadRepository = new FakeLeadProfileRepository();
        var stateRepository = new FakeManyChatContactStateRepository();
        var useCase = new ProcessManyChatEventUseCase(
            new FakeTenantRepository(tenant),
            leadRepository,
            stateRepository,
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new ProcessManyChatEventCommand(
                new ProcessManyChatEventRequest(
                    tenant.TenantId.Value,
                    "contact_001",
                    "message_received",
                    "instagram",
                    "Can you send me BOOK?",
                    "Jane",
                    "Doe",
                    "jane@rnm.test",
                    ["existing-tag"],
                    new Dictionary<string, string> { ["source"] = "instagram" },
                    "corr-manychat"),
                "corr-manychat"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("MarketingQualified", result.Value!.LeadLifecycleStage);
        Assert.Equal("leadgen-keyword-capture", result.Value.TriggeredFlow);
        Assert.Contains("leadgen-keyword", result.Value.TagsToAdd);
        Assert.Equal(LeadLifecycleStage.MarketingQualified, leadRepository.Saved!.CurrentStage);
        Assert.Equal("leadgen-keyword-capture", stateRepository.Saved!.TriggeredFlow);
    }

    [Fact]
    public async Task ExecuteAsync_UsesBookingIntentToPromoteLeadToBookingReady()
    {
        var tenant = CreateTenant();
        var useCase = new ProcessManyChatEventUseCase(
            new FakeTenantRepository(tenant),
            new FakeLeadProfileRepository(),
            new FakeManyChatContactStateRepository(),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new ProcessManyChatEventCommand(
                new ProcessManyChatEventRequest(
                    tenant.TenantId.Value,
                    "contact_002",
                    "message_received",
                    "messenger",
                    "I want to book a call this week.",
                    "Alex",
                    null,
                    null)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("BookingReady", result.Value!.LeadLifecycleStage);
        Assert.Equal("booking-agent-entry", result.Value.TriggeredFlow);
        Assert.Contains("booking-intent", result.Value.TagsToAdd);
        Assert.Equal("booking", result.Value.FieldsToUpsert["last_intent"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTenantCtaLeadsToBooking_UsesBookingHandoffFlow()
    {
        var tenant = CreateTenant(
            calendlyUrl: "https://calendly.com/rnm-growth/consultation",
            desiredAction: "Book a consultation from the content",
            contentLanguage: "Bilingual");
        var leadRepository = new FakeLeadProfileRepository();
        var stateRepository = new FakeManyChatContactStateRepository();
        var useCase = new ProcessManyChatEventUseCase(
            new FakeTenantRepository(tenant),
            leadRepository,
            stateRepository,
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new ProcessManyChatEventCommand(
                new ProcessManyChatEventRequest(
                    tenant.TenantId.Value,
                    "contact_003",
                    "message_received",
                    "instagram",
                    "Can you send me BOOK?",
                    "Maria",
                    "Lopez",
                    "maria@rnm.test")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("MarketingQualified", result.Value!.LeadLifecycleStage);
        Assert.Equal("leadgen-keyword-booking-handoff", result.Value.TriggeredFlow);
        Assert.Contains("booking-link-ready", result.Value.TagsToAdd);
        Assert.Equal("Book a consultation from the content", result.Value.FieldsToUpsert["desired_action"]);
        Assert.Equal("Bilingual", result.Value.FieldsToUpsert["content_language"]);
        Assert.Equal("https://calendly.com/rnm-growth/consultation", result.Value.FieldsToUpsert["calendly_url"]);
        Assert.Contains("booking handoff", leadRepository.Saved!.IntentSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("leadgen-keyword-booking-handoff", stateRepository.Saved!.TriggeredFlow);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesWebsiteUrlWhenTenantHasWebsite()
    {
        var tenant = CreateTenant(websiteUrl: "https://rnmgrowth.com");
        var useCase = new ProcessManyChatEventUseCase(
            new FakeTenantRepository(tenant),
            new FakeLeadProfileRepository(),
            new FakeManyChatContactStateRepository(),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new ProcessManyChatEventCommand(
                new ProcessManyChatEventRequest(
                    tenant.TenantId.Value,
                    "contact_004",
                    "message_received",
                    "instagram",
                    "Tell me more",
                    "Jane",
                    "Doe",
                    "jane@rnm.test")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://rnmgrowth.com", result.Value!.FieldsToUpsert["website_url"]);
    }

    private static Tenant CreateTenant(
        string calendlyUrl = "",
        string desiredAction = "",
        string contentLanguage = "English",
        string websiteUrl = "") =>
        Tenant.Create(
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
                ["Instagram", "Messenger"],
                ["Low engagement"],
                ["No time"],
                ["Politics"],
                CalendlyUrl: calendlyUrl,
                WebsiteUrl: websiteUrl,
                DesiredAction: desiredAction,
                ContentLanguage: contentLanguage),
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

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

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(tenant.TenantId.Value == tenantId ? tenant : null);
    }

    private sealed class FakeLeadProfileRepository : ILeadProfileRepository
    {
        public LeadProfile? Saved { get; private set; }

        public Task SaveAsync(LeadProfile leadProfile, CancellationToken cancellationToken)
        {
            Saved = leadProfile;
            return Task.CompletedTask;
        }

        public Task<LeadProfile?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved is not null && Saved.TenantId.Value == tenantId && Saved.ManyChatContactId == manyChatContactId ? Saved : null);
    }

    private sealed class FakeManyChatContactStateRepository : IManyChatContactStateRepository
    {
        public ManyChatContactState? Saved { get; private set; }

        public Task SaveAsync(ManyChatContactState contactState, CancellationToken cancellationToken)
        {
            Saved = contactState;
            return Task.CompletedTask;
        }

        public Task<ManyChatContactState?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved is not null && Saved.TenantId.Value == tenantId && Saved.ManyChatContactId == manyChatContactId ? Saved : null);
    }
}
