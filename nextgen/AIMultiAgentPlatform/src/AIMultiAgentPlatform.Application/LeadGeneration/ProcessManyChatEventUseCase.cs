using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.ManyChat;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Leads;

namespace AIMultiAgentPlatform.Application.LeadGeneration;

public sealed class ProcessManyChatEventUseCase
{
    private static readonly string[] BookingIntentKeywords =
    [
        "book",
        "booking",
        "call",
        "schedule",
        "consult",
        "demo"
    ];

    private static readonly string[] BookingIntentPhrases =
    [
        "book a call",
        "book call",
        "schedule a call",
        "schedule call",
        "book a demo",
        "schedule demo",
        "book appointment",
        "schedule appointment",
        "consultation",
        "consult call"
    ];

    private readonly ITenantRepository _tenantRepository;
    private readonly ILeadProfileRepository _leadProfileRepository;
    private readonly IManyChatContactStateRepository _manyChatContactStateRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public ProcessManyChatEventUseCase(
        ITenantRepository tenantRepository,
        ILeadProfileRepository leadProfileRepository,
        IManyChatContactStateRepository manyChatContactStateRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _leadProfileRepository = leadProfileRepository;
        _manyChatContactStateRepository = manyChatContactStateRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<ProcessManyChatEventResponse>> ExecuteAsync(
        ProcessManyChatEventCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<ProcessManyChatEventResponse>.Failure("manychat.tenant.required", "TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ManyChatContactId))
        {
            return Result<ProcessManyChatEventResponse>.Failure("manychat.contact.required", "ManyChatContactId is required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<ProcessManyChatEventResponse>.Failure("manychat.tenant.not-found", "Tenant was not found.");
        }

        var existingLead = await _leadProfileRepository.FindByContactAsync(request.TenantId, request.ManyChatContactId, cancellationToken);
        var existingState = await _manyChatContactStateRepository.FindByContactAsync(request.TenantId, request.ManyChatContactId, cancellationToken);

        var messageText = (request.MessageText ?? string.Empty).Trim();
        var ctaKeyword = tenant.Profile.CallToActionKeyword.Trim();

        var inferredStage = InferStage(messageText, ctaKeyword);
        var finalStage = existingLead is null ? inferredStage : MaxStage(existingLead.CurrentStage, inferredStage);
        var triggeredFlow = ResolveFlow(finalStage, tenant.Profile);
        var tagsToAdd = ResolveTags(finalStage, ctaKeyword, tenant.Profile);
        var fieldsToUpsert = ResolveFields(finalStage, messageText, ctaKeyword, request.Channel, tenant.Profile);
        SetIfNotEmpty(fieldsToUpsert, "source_published_content_record_id", request.SourcePublishedContentRecordId);
        SetIfNotEmpty(fieldsToUpsert, "source_platform", request.SourcePlatform);
        SetIfNotEmpty(fieldsToUpsert, "source_provider", request.SourceProviderName);
        SetIfNotEmpty(fieldsToUpsert, "source_external_post_id", request.SourceExternalPostId);

        var mergedTags = MergeTags(existingState?.Tags ?? request.CurrentTags, tagsToAdd);
        var mergedFields = MergeFields(existingState?.Fields ?? request.CurrentFields, fieldsToUpsert);

        var leadProfile = new LeadProfile(
            existingLead?.LeadProfileId ?? _idGenerator.NewId("lead"),
            tenant.TenantId,
            request.ManyChatContactId,
            NormalizeValue(request.FirstName, existingLead?.FirstName, "Unknown"),
            NormalizeValue(request.LastName, existingLead?.LastName, string.Empty),
            NormalizeValue(request.Email, existingLead?.Email, "unknown@example.invalid"),
            NormalizeValue(request.Channel, existingLead?.Channel, "ManyChat"),
            finalStage,
            BuildIntentSummary(finalStage, ctaKeyword, messageText, tenant.Profile),
            messageText,
            _clock.UtcNow,
            NormalizeValue(request.SourcePublishedContentRecordId, existingLead?.SourcePublishedContentRecordId, string.Empty),
            NormalizeValue(request.SourcePlatform, existingLead?.SourcePlatform, string.Empty),
            NormalizeValue(request.SourceProviderName, existingLead?.SourceProviderName, string.Empty),
            NormalizeValue(request.SourceExternalPostId, existingLead?.SourceExternalPostId, string.Empty));

        var contactState = new ManyChatContactState(
            existingState?.ManyChatContactStateId ?? _idGenerator.NewId("manychat_state"),
            tenant.TenantId,
            request.ManyChatContactId,
            mergedTags,
            mergedFields,
            messageText,
            triggeredFlow,
            _clock.UtcNow);

        await _leadProfileRepository.SaveAsync(leadProfile, cancellationToken);
        await _manyChatContactStateRepository.SaveAsync(contactState, cancellationToken);

        return Result<ProcessManyChatEventResponse>.Success(
            new ProcessManyChatEventResponse(
                leadProfile.LeadProfileId,
                contactState.ManyChatContactStateId,
                leadProfile.CurrentStage.ToString(),
                contactState.TriggeredFlow,
                tagsToAdd,
                fieldsToUpsert));
    }

    private static LeadLifecycleStage InferStage(string messageText, string ctaKeyword)
    {
        if (BookingIntentPhrases.Any(phrase => ContainsKeyword(messageText, phrase)))
        {
            return LeadLifecycleStage.BookingReady;
        }

        if (ContainsKeyword(messageText, ctaKeyword))
        {
            return LeadLifecycleStage.MarketingQualified;
        }

        if (BookingIntentKeywords.Any(keyword => ContainsKeyword(messageText, keyword)))
        {
            return LeadLifecycleStage.BookingReady;
        }

        return string.IsNullOrWhiteSpace(messageText) ? LeadLifecycleStage.New : LeadLifecycleStage.Engaged;
    }

    private static LeadLifecycleStage MaxStage(LeadLifecycleStage current, LeadLifecycleStage candidate) =>
        (LeadLifecycleStage)Math.Max((int)current, (int)candidate);

    private static string ResolveFlow(LeadLifecycleStage stage, Domain.Tenants.ClientProfile profile) => stage switch
    {
        LeadLifecycleStage.BookingReady => "booking-agent-entry",
        LeadLifecycleStage.MarketingQualified when RequiresBookingCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.CalendlyUrl) => "leadgen-keyword-booking-handoff",
        LeadLifecycleStage.MarketingQualified => "leadgen-keyword-capture",
        LeadLifecycleStage.Engaged => "leadgen-nurture",
        _ => "leadgen-welcome"
    };

    private static IReadOnlyList<string> ResolveTags(LeadLifecycleStage stage, string ctaKeyword, Domain.Tenants.ClientProfile profile) => stage switch
    {
        LeadLifecycleStage.BookingReady => ["booking-intent", "hot-lead"],
        LeadLifecycleStage.MarketingQualified when RequiresBookingCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.CalendlyUrl) =>
        [
            "leadgen-keyword",
            $"keyword-{ctaKeyword.ToLowerInvariant()}",
            "booking-link-ready"
        ],
        LeadLifecycleStage.MarketingQualified => ["leadgen-keyword", $"keyword-{ctaKeyword.ToLowerInvariant()}"],
        LeadLifecycleStage.Engaged => ["engaged-lead"],
        _ => ["new-lead"]
    };

    private static Dictionary<string, string> ResolveFields(
        LeadLifecycleStage stage,
        string messageText,
        string ctaKeyword,
        string channel,
        Domain.Tenants.ClientProfile profile)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lead_stage"] = stage.ToString(),
            ["last_intent"] = stage switch
            {
                LeadLifecycleStage.BookingReady => "booking",
                LeadLifecycleStage.MarketingQualified => "keyword",
                LeadLifecycleStage.Engaged => "engaged",
                _ => "new"
            },
            ["cta_keyword"] = ctaKeyword,
            ["desired_action"] = profile.DesiredAction,
            ["content_language"] = profile.ContentLanguage,
            ["last_channel"] = string.IsNullOrWhiteSpace(channel) ? "ManyChat" : channel.Trim(),
            ["last_message_excerpt"] = messageText.Length <= 120 ? messageText : messageText[..120]
        };

        if (!string.IsNullOrWhiteSpace(profile.CalendlyUrl))
        {
            fields["calendly_url"] = profile.CalendlyUrl;
        }

        if (!string.IsNullOrWhiteSpace(profile.WebsiteUrl))
        {
            fields["website_url"] = profile.WebsiteUrl;
        }

        return fields;
    }

    private static void SetIfNotEmpty(IDictionary<string, string> fields, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields[key] = value.Trim();
        }
    }

    private static string BuildIntentSummary(LeadLifecycleStage stage, string ctaKeyword, string messageText, Domain.Tenants.ClientProfile profile) => stage switch
    {
        LeadLifecycleStage.BookingReady => "Lead explicitly signaled booking intent through the ManyChat conversation.",
        LeadLifecycleStage.MarketingQualified when RequiresBookingCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.CalendlyUrl) =>
            $"Lead triggered the CTA keyword '{ctaKeyword}' and should receive a booking handoff aligned to '{profile.DesiredAction}'.",
        LeadLifecycleStage.MarketingQualified => $"Lead triggered the CTA keyword '{ctaKeyword}' and should enter the lead capture flow.",
        LeadLifecycleStage.Engaged => $"Lead replied through ManyChat and should remain in nurture. Latest message: {messageText}",
        _ => "Lead was created from ManyChat event intake."
    };

    private static bool ContainsKeyword(string message, string keyword) =>
        !string.IsNullOrWhiteSpace(keyword) &&
        message.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeValue(string? preferred, string? fallback, string defaultValue) =>
        !string.IsNullOrWhiteSpace(preferred)
            ? preferred.Trim()
            : !string.IsNullOrWhiteSpace(fallback)
                ? fallback.Trim()
                : defaultValue;

    private static IReadOnlyList<string> MergeTags(
        IReadOnlyList<string>? existingTags,
        IReadOnlyList<string> tagsToAdd) =>
        (existingTags ?? Array.Empty<string>())
            .Concat(tagsToAdd)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyDictionary<string, string> MergeFields(
        IReadOnlyDictionary<string, string>? existingFields,
        IReadOnlyDictionary<string, string> fieldsToUpsert)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (existingFields is not null)
        {
            foreach (var pair in existingFields)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        foreach (var pair in fieldsToUpsert)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static bool RequiresBookingCallToAction(Domain.Tenants.ClientProfile profile) =>
        ContainsKeyword(profile.DesiredAction, "book") ||
        ContainsKeyword(profile.DesiredAction, "consult") ||
        ContainsKeyword(profile.DesiredAction, "call") ||
        ContainsKeyword(profile.DesiredAction, "appointment");
}
