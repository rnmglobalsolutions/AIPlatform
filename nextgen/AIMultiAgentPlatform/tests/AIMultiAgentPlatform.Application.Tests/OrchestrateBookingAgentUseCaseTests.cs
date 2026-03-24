using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Booking;
using AIMultiAgentPlatform.Contracts.Booking;
using AIMultiAgentPlatform.Domain.Booking;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.FollowUps;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Reminders;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class OrchestrateBookingAgentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenBooked_CreatesReminderScheduleAndMarksLeadBooked()
    {
        var tenant = CreateTenant();
        var lead = CreateLead(tenant.TenantId, "contact_001", LeadLifecycleStage.BookingReady);
        var state = CreateState(tenant.TenantId, "contact_001");

        var useCase = CreateUseCase(
            tenant,
            lead,
            state,
            out var bookingRepository,
            out var reminderRepository,
            out var followUpRepository,
            out var leadRepository,
            out var stateRepository);

        var appointmentUtc = new DateTime(2026, 03, 25, 16, 0, 0, DateTimeKind.Utc);
        var result = await useCase.ExecuteAsync(
            new OrchestrateBookingAgentCommand(
                new OrchestrateBookingAgentRequest(
                    tenant.TenantId.Value,
                    "contact_001",
                    "Booked",
                    appointmentUtc,
                    "strategy-call",
                    ["Email", "Messenger"],
                    "corr-booking"),
                "corr-booking"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Booked", result.Value!.BookingStatus);
        Assert.Equal("Booked", result.Value.LeadLifecycleStage);
        Assert.Equal("booking-confirmed", result.Value.TriggeredFlow);
        Assert.NotNull(result.Value.ReminderScheduleId);
        Assert.Null(result.Value.FollowUpSequenceId);
        Assert.Equal(BookingStatus.Booked, bookingRepository.Saved!.Status);
        Assert.Equal(ReminderScheduleStatus.Scheduled, reminderRepository.Saved!.Status);
        Assert.Equal(4, reminderRepository.Saved.Touches.Count);
        Assert.Null(followUpRepository.Saved);
        Assert.Equal(LeadLifecycleStage.Booked, leadRepository.Saved!.CurrentStage);
        Assert.Equal("booking-confirmed", stateRepository.Saved!.TriggeredFlow);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoBooking_CreatesFollowUpSequence()
    {
        var tenant = CreateTenant();
        var lead = CreateLead(tenant.TenantId, "contact_002", LeadLifecycleStage.MarketingQualified);
        var state = CreateState(tenant.TenantId, "contact_002");

        var useCase = CreateUseCase(
            tenant,
            lead,
            state,
            out var bookingRepository,
            out var reminderRepository,
            out var followUpRepository,
            out var leadRepository,
            out var stateRepository);

        var result = await useCase.ExecuteAsync(
            new OrchestrateBookingAgentCommand(
                new OrchestrateBookingAgentRequest(
                    tenant.TenantId.Value,
                    "contact_002",
                    "NoBooking",
                    null,
                    null,
                    ["Email", "Instagram"])),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("NoBooking", result.Value!.BookingStatus);
        Assert.Equal("follow-up-sequence-start", result.Value.TriggeredFlow);
        Assert.Null(result.Value.ReminderScheduleId);
        Assert.NotNull(result.Value.FollowUpSequenceId);
        Assert.Equal(BookingStatus.NoBooking, bookingRepository.Saved!.Status);
        Assert.Null(reminderRepository.Saved);
        Assert.Equal(FollowUpSequenceStatus.Scheduled, followUpRepository.Saved!.Status);
        Assert.Equal(6, followUpRepository.Saved.Steps.Count);
        Assert.Equal(LeadLifecycleStage.MarketingQualified, leadRepository.Saved!.CurrentStage);
        Assert.Equal("follow-up-sequence-start", stateRepository.Saved!.TriggeredFlow);
    }

    private static Tenant CreateTenant() =>
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
                ["Politics"]),
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

    private static OrchestrateBookingAgentUseCase CreateUseCase(
        Tenant tenant,
        LeadProfile lead,
        ManyChatContactState state,
        out FakeBookingRecordRepository bookingRepository,
        out FakeReminderScheduleRepository reminderRepository,
        out FakeFollowUpSequenceRepository followUpRepository,
        out FakeLeadProfileRepository leadRepository,
        out FakeManyChatContactStateRepository stateRepository)
    {
        bookingRepository = new FakeBookingRecordRepository();
        reminderRepository = new FakeReminderScheduleRepository();
        followUpRepository = new FakeFollowUpSequenceRepository();
        leadRepository = new FakeLeadProfileRepository(lead);
        stateRepository = new FakeManyChatContactStateRepository(state);

        return new OrchestrateBookingAgentUseCase(
            new FakeTenantRepository(tenant),
            leadRepository,
            stateRepository,
            bookingRepository,
            reminderRepository,
            followUpRepository,
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
            Task.FromResult(Saved?.BookingRecordId == bookingRecordId ? Saved : null);
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
            Task.FromResult(Saved?.LeadProfileId == leadProfileId ? Saved : null);
    }
}
