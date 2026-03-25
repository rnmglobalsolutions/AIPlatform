using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Voice;
using AIMultiAgentPlatform.Application.Voice;
using AIMultiAgentPlatform.Contracts.Voice;
using AIMultiAgentPlatform.Domain.Booking;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.FollowUps;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Reminders;
using AIMultiAgentPlatform.Domain.Tenants;
using AIMultiAgentPlatform.Domain.Voice;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class OrchestrateVoiceAgentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenBookingCallBooksAppointment_CreatesVoiceSessionBookingAndReminder()
    {
        var tenant = CreateTenant();
        var lead = CreateLead(tenant.TenantId, "contact_001", LeadLifecycleStage.BookingReady);
        var state = CreateState(tenant.TenantId, "contact_001");

        var useCase = CreateUseCase(
            tenant,
            lead,
            state,
            new FakeVoiceConversationProvider(
                new VoiceConversationResult(
                    "ext-call-001",
                    VoiceCallStatus.Completed,
                    CallDisposition.Booked,
                    "Booked by voice.",
                    "Voice booking completed.",
                    new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 03, 23, 12, 3, 0, DateTimeKind.Utc),
                    new DateTime(2026, 03, 25, 16, 0, 0, DateTimeKind.Utc))),
            out var bookingRepository,
            out var reminderRepository,
            out var followUpRepository,
            out var leadRepository,
            out var stateRepository,
            out var voiceRepository);

        var result = await useCase.ExecuteAsync(
            new OrchestrateVoiceAgentCommand(
                new OrchestrateVoiceAgentRequest(
                    tenant.TenantId.Value,
                    "contact_001",
                    "Booking",
                    "+12145550100",
                    "rachel",
                    "corr-voice"),
                "corr-voice"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Booked", result.Value!.CallDisposition);
        Assert.Equal("Booked", result.Value.LeadLifecycleStage);
        Assert.Equal("voice-booking-confirmed", result.Value.TriggeredFlow);
        Assert.NotNull(result.Value.BookingRecordId);
        Assert.NotNull(result.Value.ReminderScheduleId);
        Assert.Null(result.Value.FollowUpSequenceId);
        Assert.Equal(LeadLifecycleStage.Booked, leadRepository.Saved!.CurrentStage);
        Assert.Equal(BookingStatus.Booked, bookingRepository.Saved!.Status);
        Assert.Equal(2, reminderRepository.Saved!.Touches.Count);
        Assert.Null(followUpRepository.Saved);
        Assert.Equal(CallDisposition.Booked, voiceRepository.Saved!.Disposition);
        Assert.Equal("voice-booking-confirmed", stateRepository.Saved!.TriggeredFlow);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFollowUpCallCompletes_CreatesFollowUpSequenceAndKeepsLeadActive()
    {
        var tenant = CreateTenant();
        var lead = CreateLead(tenant.TenantId, "contact_002", LeadLifecycleStage.MarketingQualified);
        var state = CreateState(tenant.TenantId, "contact_002");

        var useCase = CreateUseCase(
            tenant,
            lead,
            state,
            new FakeVoiceConversationProvider(
                new VoiceConversationResult(
                    "ext-call-002",
                    VoiceCallStatus.Completed,
                    CallDisposition.FollowUpCompleted,
                    "Follow-up completed by voice.",
                    "Voice follow-up kept the lead engaged.",
                    new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 03, 23, 12, 4, 0, DateTimeKind.Utc))),
            out var bookingRepository,
            out var reminderRepository,
            out var followUpRepository,
            out var leadRepository,
            out var stateRepository,
            out var voiceRepository);

        var result = await useCase.ExecuteAsync(
            new OrchestrateVoiceAgentCommand(
                new OrchestrateVoiceAgentRequest(
                    tenant.TenantId.Value,
                    "contact_002",
                    "FollowUp",
                    "+12145550101"),
                "corr-voice-follow-up"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("FollowUpCompleted", result.Value!.CallDisposition);
        Assert.Equal("voice-follow-up-complete", result.Value.TriggeredFlow);
        Assert.Null(result.Value.BookingRecordId);
        Assert.Null(result.Value.ReminderScheduleId);
        Assert.NotNull(result.Value.FollowUpSequenceId);
        Assert.Equal(LeadLifecycleStage.MarketingQualified, leadRepository.Saved!.CurrentStage);
        Assert.Null(bookingRepository.Saved);
        Assert.Null(reminderRepository.Saved);
        Assert.Equal(FollowUpSequenceStatus.Scheduled, followUpRepository.Saved!.Status);
        Assert.Equal(CallDisposition.FollowUpCompleted, voiceRepository.Saved!.Disposition);
        Assert.Equal("voice-follow-up-complete", stateRepository.Saved!.TriggeredFlow);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTenantHasCalendlyAndBilingualBookingGoal_UsesThatContextInVoiceFlow()
    {
        var tenant = CreateTenant(
            calendlyUrl: "https://calendly.com/rnm-growth/consultation",
            desiredAction: "Book a consultation from the content",
            contentLanguage: "Bilingual",
            brandTone: "Professional");
        var lead = CreateLead(tenant.TenantId, "contact_003", LeadLifecycleStage.BookingReady);
        var state = CreateState(tenant.TenantId, "contact_003");
        var voiceProvider = new FakeVoiceConversationProvider(
            new VoiceConversationResult(
                "ext-call-003",
                VoiceCallStatus.Completed,
                CallDisposition.NoAnswer,
                "No answer on the bilingual booking call.",
                "Voice booking handoff still needs completion.",
                new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 03, 23, 12, 2, 0, DateTimeKind.Utc)));

        var useCase = CreateUseCase(
            tenant,
            lead,
            state,
            voiceProvider,
            out var bookingRepository,
            out _,
            out _,
            out _,
            out var stateRepository,
            out _);

        var result = await useCase.ExecuteAsync(
            new OrchestrateVoiceAgentCommand(
                new OrchestrateVoiceAgentRequest(
                    tenant.TenantId.Value,
                    "contact_003",
                    "Booking",
                    "+12145550102"),
                "corr-voice-bilingual"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("https://calendly.com/rnm-growth/consultation", voiceProvider.LastRequest!.PromptSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bilingual", voiceProvider.LastRequest.PromptSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("professional", voiceProvider.LastRequest.PromptSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("https://calendly.com/rnm-growth/consultation", bookingRepository.Saved!.CalendlyUrl);
        Assert.Equal("Book a consultation from the content", result.Value!.FieldsToUpsert["desired_action"]);
        Assert.Equal("Bilingual", result.Value.FieldsToUpsert["content_language"]);
        Assert.Equal("https://calendly.com/rnm-growth/consultation", result.Value.FieldsToUpsert["calendly_url"]);
        Assert.Equal("voice-booking-handoff", stateRepository.Saved!.TriggeredFlow);
    }

    private static Tenant CreateTenant(
        string calendlyUrl = "",
        string desiredAction = "",
        string contentLanguage = "English",
        string brandTone = "Bold") =>
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
                brandTone,
                "BOOK",
                ["Instagram", "Messenger"],
                ["Low engagement"],
                ["No time"],
                ["Politics"],
                CalendlyUrl: calendlyUrl,
                DesiredAction: desiredAction,
                ContentLanguage: contentLanguage),
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

    private static LeadProfile CreateLead(TenantId tenantId, string contactId, LeadLifecycleStage stage) =>
        new(
            "lead_001",
            tenantId,
            contactId,
            "Jane",
            "Doe",
            "jane@rnm.test",
            "Instagram",
            stage,
            "Intent summary",
            "latest message",
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

    private static ManyChatContactState CreateState(TenantId tenantId, string contactId) =>
        new(
            "manychat_001",
            tenantId,
            contactId,
            ["engaged-lead"],
            new Dictionary<string, string> { ["source"] = "manychat" },
            "latest message",
            "leadgen-nurture",
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

    private static OrchestrateVoiceAgentUseCase CreateUseCase(
        Tenant tenant,
        LeadProfile lead,
        ManyChatContactState state,
        IVoiceConversationProvider voiceProvider,
        out FakeBookingRecordRepository bookingRepository,
        out FakeReminderScheduleRepository reminderRepository,
        out FakeFollowUpSequenceRepository followUpRepository,
        out FakeLeadProfileRepository leadRepository,
        out FakeManyChatContactStateRepository stateRepository,
        out FakeVoiceCallSessionRepository voiceRepository)
    {
        bookingRepository = new FakeBookingRecordRepository();
        reminderRepository = new FakeReminderScheduleRepository();
        followUpRepository = new FakeFollowUpSequenceRepository();
        leadRepository = new FakeLeadProfileRepository(lead);
        stateRepository = new FakeManyChatContactStateRepository(state);
        voiceRepository = new FakeVoiceCallSessionRepository();

        return new OrchestrateVoiceAgentUseCase(
            new FakeTenantRepository(tenant),
            leadRepository,
            stateRepository,
            bookingRepository,
            reminderRepository,
            followUpRepository,
            voiceRepository,
            voiceProvider,
            new DeterministicIdGenerator(),
            new FixedClock());
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

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(tenant.TenantId.Value == tenantId ? tenant : null);
    }

    private sealed class FakeLeadProfileRepository(LeadProfile seed) : ILeadProfileRepository
    {
        public LeadProfile? Saved { get; private set; }
        public Task SaveAsync(LeadProfile leadProfile, CancellationToken cancellationToken)
        {
            Saved = leadProfile;
            return Task.CompletedTask;
        }

        public Task<LeadProfile?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken)
        {
            var current = Saved ?? seed;
            return Task.FromResult(current.TenantId.Value == tenantId && current.ManyChatContactId == manyChatContactId ? current : null);
        }
    }

    private sealed class FakeManyChatContactStateRepository(ManyChatContactState seed) : IManyChatContactStateRepository
    {
        public ManyChatContactState? Saved { get; private set; }
        public Task SaveAsync(ManyChatContactState contactState, CancellationToken cancellationToken)
        {
            Saved = contactState;
            return Task.CompletedTask;
        }

        public Task<ManyChatContactState?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken)
        {
            var current = Saved ?? seed;
            return Task.FromResult(current.TenantId.Value == tenantId && current.ManyChatContactId == manyChatContactId ? current : null);
        }
    }

    private sealed class FakeBookingRecordRepository : IBookingRecordRepository
    {
        public BookingRecord? Saved { get; private set; }
        public Task SaveAsync(BookingRecord bookingRecord, CancellationToken cancellationToken)
        {
            Saved = bookingRecord;
            return Task.CompletedTask;
        }

        public Task<BookingRecord?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved is not null && Saved.TenantId.Value == tenantId && Saved.ManyChatContactId == manyChatContactId ? Saved : null);
    }

    private sealed class FakeReminderScheduleRepository : IReminderScheduleRepository
    {
        public ReminderSchedule? Saved { get; private set; }
        public Task SaveAsync(ReminderSchedule reminderSchedule, CancellationToken cancellationToken)
        {
            Saved = reminderSchedule;
            return Task.CompletedTask;
        }

        public Task<ReminderSchedule?> FindByBookingRecordAsync(string bookingRecordId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved is not null && Saved.BookingRecordId == bookingRecordId ? Saved : null);
    }

    private sealed class FakeFollowUpSequenceRepository : IFollowUpSequenceRepository
    {
        public FollowUpSequence? Saved { get; private set; }
        public Task SaveAsync(FollowUpSequence followUpSequence, CancellationToken cancellationToken)
        {
            Saved = followUpSequence;
            return Task.CompletedTask;
        }

        public Task<FollowUpSequence?> FindByLeadProfileAsync(string leadProfileId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved is not null && Saved.LeadProfileId == leadProfileId ? Saved : null);
    }

    private sealed class FakeVoiceCallSessionRepository : IVoiceCallSessionRepository
    {
        public VoiceCallSession? Saved { get; private set; }
        public Task SaveAsync(VoiceCallSession voiceCallSession, CancellationToken cancellationToken)
        {
            Saved = voiceCallSession;
            return Task.CompletedTask;
        }

        public Task<VoiceCallSession?> FindByIdAsync(string voiceCallSessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved is not null && Saved.VoiceCallSessionId == voiceCallSessionId ? Saved : null);
    }

    private sealed class FakeVoiceConversationProvider(VoiceConversationResult result) : IVoiceConversationProvider
    {
        public VoiceConversationRequest? LastRequest { get; private set; }

        public Task<VoiceConversationResult> ExecuteAsync(VoiceConversationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Capture(request));

        private VoiceConversationResult Capture(VoiceConversationRequest request)
        {
            LastRequest = request;
            return result;
        }
    }
}
