using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Voice;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Voice;
using AIMultiAgentPlatform.Domain.Booking;
using AIMultiAgentPlatform.Domain.Communications;
using AIMultiAgentPlatform.Domain.FollowUps;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Reminders;
using AIMultiAgentPlatform.Domain.Voice;

namespace AIMultiAgentPlatform.Application.Voice;

public sealed class OrchestrateVoiceAgentUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILeadProfileRepository _leadProfileRepository;
    private readonly IManyChatContactStateRepository _manyChatContactStateRepository;
    private readonly IBookingRecordRepository _bookingRecordRepository;
    private readonly IReminderScheduleRepository _reminderScheduleRepository;
    private readonly IFollowUpSequenceRepository _followUpSequenceRepository;
    private readonly IVoiceCallSessionRepository _voiceCallSessionRepository;
    private readonly IVoiceConversationProvider _voiceConversationProvider;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public OrchestrateVoiceAgentUseCase(
        ITenantRepository tenantRepository,
        ILeadProfileRepository leadProfileRepository,
        IManyChatContactStateRepository manyChatContactStateRepository,
        IBookingRecordRepository bookingRecordRepository,
        IReminderScheduleRepository reminderScheduleRepository,
        IFollowUpSequenceRepository followUpSequenceRepository,
        IVoiceCallSessionRepository voiceCallSessionRepository,
        IVoiceConversationProvider voiceConversationProvider,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _leadProfileRepository = leadProfileRepository;
        _manyChatContactStateRepository = manyChatContactStateRepository;
        _bookingRecordRepository = bookingRecordRepository;
        _reminderScheduleRepository = reminderScheduleRepository;
        _followUpSequenceRepository = followUpSequenceRepository;
        _voiceCallSessionRepository = voiceCallSessionRepository;
        _voiceConversationProvider = voiceConversationProvider;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<OrchestrateVoiceAgentResponse>> ExecuteAsync(
        OrchestrateVoiceAgentCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<OrchestrateVoiceAgentResponse>.Failure("voice.tenant.required", "TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ManyChatContactId))
        {
            return Result<OrchestrateVoiceAgentResponse>.Failure("voice.contact.required", "ManyChatContactId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return Result<OrchestrateVoiceAgentResponse>.Failure("voice.phone.required", "PhoneNumber is required.");
        }

        if (!Enum.TryParse<VoiceCallObjective>(request.Objective?.Trim(), true, out var objective))
        {
            return Result<OrchestrateVoiceAgentResponse>.Failure("voice.objective.invalid", "Objective must be Qualification, Booking, Reminder, or FollowUp.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<OrchestrateVoiceAgentResponse>.Failure("voice.tenant.not-found", "Tenant was not found.");
        }

        var lead = await _leadProfileRepository.FindByContactAsync(request.TenantId, request.ManyChatContactId, cancellationToken);
        if (lead is null)
        {
            return Result<OrchestrateVoiceAgentResponse>.Failure("voice.lead.not-found", "Lead was not found. Process a ManyChat event first.");
        }

        var manyChatState = await _manyChatContactStateRepository.FindByContactAsync(request.TenantId, request.ManyChatContactId, cancellationToken);
        var existingBooking = await _bookingRecordRepository.FindByContactAsync(request.TenantId, request.ManyChatContactId, cancellationToken);

        if (objective == VoiceCallObjective.Reminder && existingBooking is null)
        {
            return Result<OrchestrateVoiceAgentResponse>.Failure("voice.reminder.booking-required", "Reminder voice calls require an existing booking record.");
        }

        var providerRequest = new VoiceConversationRequest(
            tenant.TenantId.Value,
            lead.ManyChatContactId,
            lead.LeadProfileId,
            $"{lead.FirstName} {lead.LastName}".Trim(),
            request.PhoneNumber.Trim(),
            objective,
            BuildPromptSummary(tenant.Profile, lead, objective),
            request.PreferredVoiceId);

        var providerResult = await _voiceConversationProvider.ExecuteAsync(providerRequest, cancellationToken);
        var callSession = new VoiceCallSession(
            _idGenerator.NewId("voice_call"),
            tenant.TenantId,
            lead.LeadProfileId,
            lead.ManyChatContactId,
            objective,
            providerResult.Status,
            providerResult.Disposition,
            request.PhoneNumber.Trim(),
            providerResult.ExternalCallId,
            providerResult.Transcript,
            providerResult.Summary,
            providerResult.StartedUtc,
            providerResult.CompletedUtc);

        var now = _clock.UtcNow;
        var bookingRecord = existingBooking;
        ReminderSchedule? reminderSchedule = null;
        FollowUpSequence? followUpSequence = null;
        var finalStage = lead.CurrentStage;
        string triggeredFlow;
        IReadOnlyList<string> tagsToAdd;
        IReadOnlyDictionary<string, string> fieldsToUpsert;

        switch (objective)
        {
            case VoiceCallObjective.Qualification:
                finalStage = providerResult.Disposition == CallDisposition.Qualified
                    ? MaxStage(lead.CurrentStage, LeadLifecycleStage.MarketingQualified)
                    : lead.CurrentStage;
                triggeredFlow = providerResult.Disposition == CallDisposition.Qualified
                    ? "voice-qualified-follow-up"
                    : "voice-human-review";
                tagsToAdd = providerResult.Disposition == CallDisposition.Qualified
                    ? ["voice-qualified", "leadgen-qualified"]
                    : ["voice-human-follow-up"];
                fieldsToUpsert = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["voice_last_objective"] = objective.ToString(),
                    ["voice_last_disposition"] = providerResult.Disposition.ToString(),
                    ["desired_action"] = tenant.Profile.DesiredAction,
                    ["content_language"] = tenant.Profile.ContentLanguage,
                    ["lead_stage"] = finalStage.ToString()
                };
                break;

            case VoiceCallObjective.Booking:
                if (providerResult.Disposition == CallDisposition.Booked)
                {
                    finalStage = LeadLifecycleStage.Booked;
                    triggeredFlow = "voice-booking-confirmed";
                    bookingRecord = new BookingRecord(
                        _idGenerator.NewId("booking"),
                        tenant.TenantId,
                        lead.LeadProfileId,
                        lead.ManyChatContactId,
                        BookingStatus.Booked,
                        ResolveBookingReferenceUrl(tenant.Profile, providerResult.ExternalCallId),
                        "voice-booking",
                        providerResult.AppointmentUtc ?? now.AddDays(2),
                        now,
                        lead.SourcePublishedContentRecordId,
                        lead.SourcePlatform,
                        lead.SourceProviderName);
                    reminderSchedule = BuildImmediateVoiceReminderSchedule(tenant.TenantId, bookingRecord, providerResult.AppointmentUtc ?? now.AddDays(2));
                    tagsToAdd = ["voice-booked", "appointment-booked", "voice-reminders-scheduled"];
                    fieldsToUpsert = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["voice_last_objective"] = objective.ToString(),
                        ["voice_last_disposition"] = providerResult.Disposition.ToString(),
                        ["booking_status"] = bookingRecord.Status.ToString(),
                        ["desired_action"] = tenant.Profile.DesiredAction,
                        ["content_language"] = tenant.Profile.ContentLanguage,
                        ["calendly_url"] = bookingRecord.CalendlyUrl,
                        ["appointment_utc"] = bookingRecord.AppointmentUtc?.ToString("O") ?? string.Empty,
                        ["lead_stage"] = finalStage.ToString()
                    };
                }
                else
                {
                    finalStage = MaxStage(lead.CurrentStage, LeadLifecycleStage.BookingReady);
                    triggeredFlow = "voice-booking-handoff";
                    bookingRecord = new BookingRecord(
                        _idGenerator.NewId("booking"),
                        tenant.TenantId,
                        lead.LeadProfileId,
                        lead.ManyChatContactId,
                        BookingStatus.Requested,
                        ResolveBookingReferenceUrl(tenant.Profile, providerResult.ExternalCallId),
                        "voice-booking",
                        null,
                        now,
                        lead.SourcePublishedContentRecordId,
                        lead.SourcePlatform,
                        lead.SourceProviderName);
                    followUpSequence = BuildVoiceFollowUpSequence(tenant.TenantId, lead.LeadProfileId, "Voice booking handoff needs completion.");
                    tagsToAdd = ["voice-booking-intent", "booking-link-sent"];
                    fieldsToUpsert = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["voice_last_objective"] = objective.ToString(),
                        ["voice_last_disposition"] = providerResult.Disposition.ToString(),
                        ["booking_status"] = bookingRecord.Status.ToString(),
                        ["desired_action"] = tenant.Profile.DesiredAction,
                        ["content_language"] = tenant.Profile.ContentLanguage,
                        ["calendly_url"] = bookingRecord.CalendlyUrl,
                        ["lead_stage"] = finalStage.ToString()
                    };
                }
                break;

            case VoiceCallObjective.Reminder:
                finalStage = lead.CurrentStage;
                triggeredFlow = "voice-reminder-complete";
                reminderSchedule = await _reminderScheduleRepository.FindByBookingRecordAsync(existingBooking!.BookingRecordId, cancellationToken)
                    ?? BuildImmediateVoiceReminderSchedule(tenant.TenantId, existingBooking, existingBooking.AppointmentUtc ?? now.AddDays(1));
                tagsToAdd = ["voice-reminder-delivered"];
                fieldsToUpsert = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["voice_last_objective"] = objective.ToString(),
                    ["voice_last_disposition"] = providerResult.Disposition.ToString(),
                    ["desired_action"] = tenant.Profile.DesiredAction,
                    ["content_language"] = tenant.Profile.ContentLanguage,
                    ["last_reminder_channel"] = CommunicationChannel.Voice.ToString(),
                    ["lead_stage"] = finalStage.ToString()
                };
                break;

            default:
                finalStage = MaxStage(lead.CurrentStage, LeadLifecycleStage.MarketingQualified);
                triggeredFlow = "voice-follow-up-complete";
                followUpSequence = await _followUpSequenceRepository.FindByLeadProfileAsync(lead.LeadProfileId, cancellationToken)
                    ?? BuildVoiceFollowUpSequence(tenant.TenantId, lead.LeadProfileId, "Voice follow-up was completed.");
                tagsToAdd = ["voice-follow-up-complete"];
                fieldsToUpsert = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["voice_last_objective"] = objective.ToString(),
                    ["voice_last_disposition"] = providerResult.Disposition.ToString(),
                    ["desired_action"] = tenant.Profile.DesiredAction,
                    ["content_language"] = tenant.Profile.ContentLanguage,
                    ["last_follow_up_channel"] = CommunicationChannel.Voice.ToString(),
                    ["lead_stage"] = finalStage.ToString()
                };
                break;
        }

        var updatedLead = lead with
        {
            CurrentStage = finalStage,
            IntentSummary = providerResult.Summary,
            LastMessageText = providerResult.Transcript.Length <= 240 ? providerResult.Transcript : providerResult.Transcript[..240],
            UpdatedUtc = now
        };

        var updatedState = new ManyChatContactState(
            manyChatState?.ManyChatContactStateId ?? _idGenerator.NewId("manychat_state"),
            tenant.TenantId,
            lead.ManyChatContactId,
            MergeTags(manyChatState?.Tags, tagsToAdd),
            MergeFields(manyChatState?.Fields, fieldsToUpsert),
            providerResult.Transcript,
            triggeredFlow,
            now);

        await _voiceCallSessionRepository.SaveAsync(callSession, cancellationToken);
        await _leadProfileRepository.SaveAsync(updatedLead, cancellationToken);
        await _manyChatContactStateRepository.SaveAsync(updatedState, cancellationToken);

        if (bookingRecord is not null && (objective == VoiceCallObjective.Booking || objective == VoiceCallObjective.Reminder))
        {
            await _bookingRecordRepository.SaveAsync(bookingRecord, cancellationToken);
        }

        if (reminderSchedule is not null)
        {
            await _reminderScheduleRepository.SaveAsync(reminderSchedule, cancellationToken);
        }

        if (followUpSequence is not null)
        {
            await _followUpSequenceRepository.SaveAsync(followUpSequence, cancellationToken);
        }

        return Result<OrchestrateVoiceAgentResponse>.Success(
            new OrchestrateVoiceAgentResponse(
                callSession.VoiceCallSessionId,
                callSession.ExternalCallId,
                objective.ToString(),
                callSession.Status.ToString(),
                callSession.Disposition.ToString(),
                updatedLead.CurrentStage.ToString(),
                updatedState.TriggeredFlow,
                bookingRecord?.BookingRecordId,
                reminderSchedule?.ReminderScheduleId,
                followUpSequence?.FollowUpSequenceId,
                tagsToAdd,
                fieldsToUpsert,
                providerResult.Transcript.Length <= 160 ? providerResult.Transcript : providerResult.Transcript[..160]));
    }

    private ReminderSchedule BuildImmediateVoiceReminderSchedule(
        Domain.Common.TenantId tenantId,
        BookingRecord bookingRecord,
        DateTime appointmentUtc)
    {
        ReminderTouch[] touches =
        [
            new ReminderTouch(CommunicationChannel.Voice, appointmentUtc.AddHours(-24), "voice-reminder-24h"),
            new ReminderTouch(CommunicationChannel.Voice, appointmentUtc.AddHours(-1), "voice-reminder-1h")
        ];

        return new ReminderSchedule(
            _idGenerator.NewId("reminder"),
            tenantId,
            bookingRecord.BookingRecordId,
            ReminderScheduleStatus.Scheduled,
            touches,
            _clock.UtcNow);
    }

    private FollowUpSequence BuildVoiceFollowUpSequence(
        Domain.Common.TenantId tenantId,
        string leadProfileId,
        string reason)
    {
        var now = _clock.UtcNow;
        FollowUpStep[] steps =
        [
            new FollowUpStep(CommunicationChannel.Voice, now, "voice-follow-up-now"),
            new FollowUpStep(CommunicationChannel.Messenger, now.AddDays(1), "message-follow-up-day-1")
        ];

        return new FollowUpSequence(
            _idGenerator.NewId("followup"),
            tenantId,
            leadProfileId,
            FollowUpSequenceStatus.Scheduled,
            reason,
            steps,
            now);
    }

    private static LeadLifecycleStage MaxStage(LeadLifecycleStage current, LeadLifecycleStage candidate) =>
        (LeadLifecycleStage)Math.Max((int)current, (int)candidate);

    private static string BuildPromptSummary(Domain.Tenants.ClientProfile profile, LeadProfile lead, VoiceCallObjective objective) => objective switch
    {
        VoiceCallObjective.Qualification => $"Qualify the lead, understand intent, and tie the conversation back to CTA keyword '{profile.CallToActionKeyword}'. Use a {profile.BrandTone.ToLowerInvariant()} tone and speak in {profile.ContentLanguage.ToLowerInvariant()}. Reference {ResolveWebsiteReference(profile)} if the lead asks for the main business site.",
        VoiceCallObjective.Booking => $"Help the lead book the next appointment after confirming fit. Current lead stage: {lead.CurrentStage}. The desired action is '{profile.DesiredAction}'. Use {ResolvePromptCalendlyReference(profile)} when a booking link is needed and speak in a {profile.BrandTone.ToLowerInvariant()} {profile.ContentLanguage.ToLowerInvariant()} style.",
        VoiceCallObjective.Reminder => $"Deliver the appointment reminder, confirm attendance, and keep the delivery {profile.BrandTone.ToLowerInvariant()} and {profile.ContentLanguage.ToLowerInvariant()}.",
        _ => $"Re-engage the lead, resolve objections, and move them toward '{profile.DesiredAction}' while using a {profile.BrandTone.ToLowerInvariant()} {profile.ContentLanguage.ToLowerInvariant()} tone. Reference {ResolveWebsiteReference(profile)} if they ask where to learn more."
    };

    private static string BuildVoiceReferenceUrl(string externalCallId) => $"https://voice-agent.local/calls/{externalCallId}";

    private static string ResolveBookingReferenceUrl(Domain.Tenants.ClientProfile profile, string externalCallId) =>
        string.IsNullOrWhiteSpace(profile.CalendlyUrl)
            ? BuildVoiceReferenceUrl(externalCallId)
            : profile.CalendlyUrl;

    private static string ResolvePromptCalendlyReference(Domain.Tenants.ClientProfile profile) =>
        string.IsNullOrWhiteSpace(profile.CalendlyUrl) ? "the configured booking link" : profile.CalendlyUrl;

    private static string ResolveWebsiteReference(Domain.Tenants.ClientProfile profile) =>
        string.IsNullOrWhiteSpace(profile.WebsiteUrl) ? "the configured business website" : profile.WebsiteUrl;

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
