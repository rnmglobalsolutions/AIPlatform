using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Booking;
using AIMultiAgentPlatform.Domain.Booking;
using AIMultiAgentPlatform.Domain.Communications;
using AIMultiAgentPlatform.Domain.FollowUps;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Reminders;

namespace AIMultiAgentPlatform.Application.Booking;

public sealed class OrchestrateBookingAgentUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILeadProfileRepository _leadProfileRepository;
    private readonly IManyChatContactStateRepository _manyChatContactStateRepository;
    private readonly IBookingRecordRepository _bookingRecordRepository;
    private readonly IReminderScheduleRepository _reminderScheduleRepository;
    private readonly IFollowUpSequenceRepository _followUpSequenceRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public OrchestrateBookingAgentUseCase(
        ITenantRepository tenantRepository,
        ILeadProfileRepository leadProfileRepository,
        IManyChatContactStateRepository manyChatContactStateRepository,
        IBookingRecordRepository bookingRecordRepository,
        IReminderScheduleRepository reminderScheduleRepository,
        IFollowUpSequenceRepository followUpSequenceRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _leadProfileRepository = leadProfileRepository;
        _manyChatContactStateRepository = manyChatContactStateRepository;
        _bookingRecordRepository = bookingRecordRepository;
        _reminderScheduleRepository = reminderScheduleRepository;
        _followUpSequenceRepository = followUpSequenceRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<OrchestrateBookingAgentResponse>> ExecuteAsync(
        OrchestrateBookingAgentCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<OrchestrateBookingAgentResponse>.Failure("booking.tenant.required", "TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ManyChatContactId))
        {
            return Result<OrchestrateBookingAgentResponse>.Failure("booking.contact.required", "ManyChatContactId is required.");
        }

        if (!Enum.TryParse<BookingStatus>(request.Outcome?.Trim(), true, out var outcome))
        {
            return Result<OrchestrateBookingAgentResponse>.Failure("booking.outcome.invalid", "Outcome must be Requested, Booked, or NoBooking.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<OrchestrateBookingAgentResponse>.Failure("booking.tenant.not-found", "Tenant was not found.");
        }

        var lead = await _leadProfileRepository.FindByContactAsync(request.TenantId, request.ManyChatContactId, cancellationToken);
        if (lead is null)
        {
            return Result<OrchestrateBookingAgentResponse>.Failure("booking.lead.not-found", "Lead was not found. Process a ManyChat event first.");
        }

        var manyChatState = await _manyChatContactStateRepository.FindByContactAsync(request.TenantId, request.ManyChatContactId, cancellationToken);
        var calendlyEventType = string.IsNullOrWhiteSpace(request.CalendlyEventType) ? "discovery-call" : request.CalendlyEventType.Trim();
        var calendlyUrl = $"https://calendly.com/{tenant.Slug}/{calendlyEventType}?contact={Uri.EscapeDataString(lead.ManyChatContactId)}";

        if (outcome == BookingStatus.Booked && request.AppointmentUtc is null)
        {
            return Result<OrchestrateBookingAgentResponse>.Failure("booking.appointment.required", "AppointmentUtc is required when outcome is Booked.");
        }

        var bookingRecord = new BookingRecord(
            _idGenerator.NewId("booking"),
            tenant.TenantId,
            lead.LeadProfileId,
            lead.ManyChatContactId,
            outcome,
            calendlyUrl,
            calendlyEventType,
            request.AppointmentUtc,
            _clock.UtcNow);

        var preferredChannels = ResolvePreferredChannels(request.PreferredChannels, lead.Channel, !lead.Email.EndsWith("@example.invalid", StringComparison.OrdinalIgnoreCase));

        ReminderSchedule? reminderSchedule = null;
        FollowUpSequence? followUpSequence = null;
        LeadLifecycleStage finalStage;
        string triggeredFlow;
        IReadOnlyList<string> tagsToAdd;
        IReadOnlyDictionary<string, string> fieldsToUpsert;

        switch (outcome)
        {
            case BookingStatus.Requested:
                finalStage = LeadLifecycleStage.BookingReady;
                triggeredFlow = "booking-agent-entry";
                tagsToAdd = ["booking-link-sent", "booking-intent"];
                fieldsToUpsert = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["booking_status"] = "Requested",
                    ["calendly_url"] = calendlyUrl,
                    ["lead_stage"] = finalStage.ToString()
                };
                break;

            case BookingStatus.Booked:
                finalStage = LeadLifecycleStage.Booked;
                triggeredFlow = "booking-confirmed";
                tagsToAdd = ["appointment-booked", "reminders-scheduled"];
                reminderSchedule = BuildReminderSchedule(tenant.TenantId, bookingRecord, preferredChannels, request.AppointmentUtc!.Value);
                fieldsToUpsert = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["booking_status"] = "Booked",
                    ["appointment_utc"] = request.AppointmentUtc.Value.ToString("O"),
                    ["reminder_channels"] = string.Join(",", reminderSchedule.Touches.Select(t => t.Channel)),
                    ["lead_stage"] = finalStage.ToString()
                };
                break;

            default:
                finalStage = lead.CurrentStage >= LeadLifecycleStage.MarketingQualified ? lead.CurrentStage : LeadLifecycleStage.MarketingQualified;
                triggeredFlow = "follow-up-sequence-start";
                tagsToAdd = ["follow-up-active", "booking-not-completed"];
                followUpSequence = BuildFollowUpSequence(tenant.TenantId, lead.LeadProfileId, preferredChannels);
                fieldsToUpsert = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["booking_status"] = "NoBooking",
                    ["follow_up_channels"] = string.Join(",", followUpSequence.Steps.Select(step => step.Channel)),
                    ["lead_stage"] = finalStage.ToString()
                };
                break;
        }

        var updatedLead = lead with
        {
            CurrentStage = finalStage,
            IntentSummary = outcome switch
            {
                BookingStatus.Requested => "Calendly handoff was triggered for the lead.",
                BookingStatus.Booked => "Lead booked an appointment and entered reminder orchestration.",
                _ => "Lead did not book yet and entered follow-up orchestration."
            },
            UpdatedUtc = _clock.UtcNow
        };

        var updatedState = new ManyChatContactState(
            manyChatState?.ManyChatContactStateId ?? _idGenerator.NewId("manychat_state"),
            tenant.TenantId,
            lead.ManyChatContactId,
            MergeTags(manyChatState?.Tags, tagsToAdd),
            MergeFields(manyChatState?.Fields, fieldsToUpsert),
            manyChatState?.LastInboundText ?? string.Empty,
            triggeredFlow,
            _clock.UtcNow);

        await _bookingRecordRepository.SaveAsync(bookingRecord, cancellationToken);
        await _leadProfileRepository.SaveAsync(updatedLead, cancellationToken);
        await _manyChatContactStateRepository.SaveAsync(updatedState, cancellationToken);

        if (reminderSchedule is not null)
        {
            await _reminderScheduleRepository.SaveAsync(reminderSchedule, cancellationToken);
        }

        if (followUpSequence is not null)
        {
            await _followUpSequenceRepository.SaveAsync(followUpSequence, cancellationToken);
        }

        return Result<OrchestrateBookingAgentResponse>.Success(
            new OrchestrateBookingAgentResponse(
                updatedLead.LeadProfileId,
                bookingRecord.BookingRecordId,
                bookingRecord.Status.ToString(),
                bookingRecord.CalendlyUrl,
                reminderSchedule?.ReminderScheduleId,
                followUpSequence?.FollowUpSequenceId,
                updatedLead.CurrentStage.ToString(),
                updatedState.TriggeredFlow,
                tagsToAdd,
                fieldsToUpsert));
    }

    private ReminderSchedule BuildReminderSchedule(
        Domain.Common.TenantId tenantId,
        BookingRecord bookingRecord,
        IReadOnlyList<CommunicationChannel> channels,
        DateTime appointmentUtc)
    {
        var touches = channels
            .SelectMany(channel => new[]
            {
                new ReminderTouch(channel, appointmentUtc.AddHours(-24), "appointment-reminder-24h"),
                new ReminderTouch(channel, appointmentUtc.AddHours(-1), "appointment-reminder-1h")
            })
            .OrderBy(touch => touch.ScheduledUtc)
            .ToArray();

        return new ReminderSchedule(
            _idGenerator.NewId("reminder"),
            tenantId,
            bookingRecord.BookingRecordId,
            ReminderScheduleStatus.Scheduled,
            touches,
            _clock.UtcNow);
    }

    private FollowUpSequence BuildFollowUpSequence(
        Domain.Common.TenantId tenantId,
        string leadProfileId,
        IReadOnlyList<CommunicationChannel> channels)
    {
        var now = _clock.UtcNow;
        var steps = channels
            .SelectMany(channel => new[]
            {
                new FollowUpStep(channel, now.AddDays(1), "follow-up-day-1"),
                new FollowUpStep(channel, now.AddDays(3), "follow-up-day-3"),
                new FollowUpStep(channel, now.AddDays(7), "follow-up-day-7")
            })
            .OrderBy(step => step.ScheduledUtc)
            .ToArray();

        return new FollowUpSequence(
            _idGenerator.NewId("followup"),
            tenantId,
            leadProfileId,
            FollowUpSequenceStatus.Scheduled,
            "Lead did not book after the Calendly handoff.",
            steps,
            _clock.UtcNow);
    }

    private static IReadOnlyList<CommunicationChannel> ResolvePreferredChannels(
        IReadOnlyList<string>? preferredChannels,
        string leadChannel,
        bool hasValidEmail)
    {
        var channels = new List<CommunicationChannel>();

        if (preferredChannels is { Count: > 0 })
        {
            foreach (var channel in preferredChannels)
            {
                if (Enum.TryParse<CommunicationChannel>(channel, true, out var parsed))
                {
                    channels.Add(parsed);
                }
            }
        }

        if (channels.Count == 0)
        {
            if (hasValidEmail)
            {
                channels.Add(CommunicationChannel.Email);
            }

            if (leadChannel.Contains("instagram", StringComparison.OrdinalIgnoreCase))
            {
                channels.Add(CommunicationChannel.Instagram);
            }
            else
            {
                channels.Add(CommunicationChannel.Messenger);
            }
        }

        return channels.Distinct().ToArray();
    }

    private static IReadOnlyList<string> MergeTags(IReadOnlyList<string>? existing, IReadOnlyList<string> add) =>
        (existing ?? Array.Empty<string>())
            .Concat(add)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyDictionary<string, string> MergeFields(
        IReadOnlyDictionary<string, string>? existing,
        IReadOnlyDictionary<string, string> upsert)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (existing is not null)
        {
            foreach (var pair in existing)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        foreach (var pair in upsert)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }
}
