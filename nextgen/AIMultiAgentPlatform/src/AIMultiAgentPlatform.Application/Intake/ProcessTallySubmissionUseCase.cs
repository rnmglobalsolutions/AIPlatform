using System.Text.RegularExpressions;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Intake;
using AIMultiAgentPlatform.Domain.Strategy;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Intake;

public sealed class ProcessTallySubmissionUseCase
{
    private enum ConversionMode
    {
        Booking,
        Website,
        DirectMessage,
        CommentKeyword,
        Generic
    }

    private static readonly ContentCategory[] CategoryRotation =
    {
        ContentCategory.PainPoint,
        ContentCategory.Mistake,
        ContentCategory.MythBusting,
        ContentCategory.Faq,
        ContentCategory.ObjectionHandling,
        ContentCategory.Authority,
        ContentCategory.Story,
        ContentCategory.Comparison,
        ContentCategory.CtaDriven,
        ContentCategory.Urgency
    };

    private readonly ITenantRepository _tenantRepository;
    private readonly ITallySubmissionReceiptRepository _tallySubmissionReceiptRepository;
    private readonly IStrategyPlanRepository _strategyPlanRepository;
    private readonly IEditorialBacklogRepository _editorialBacklogRepository;
    private readonly IClock _clock;

    public ProcessTallySubmissionUseCase(
        ITenantRepository tenantRepository,
        ITallySubmissionReceiptRepository tallySubmissionReceiptRepository,
        IStrategyPlanRepository strategyPlanRepository,
        IEditorialBacklogRepository editorialBacklogRepository,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _tallySubmissionReceiptRepository = tallySubmissionReceiptRepository;
        _strategyPlanRepository = strategyPlanRepository;
        _editorialBacklogRepository = editorialBacklogRepository;
        _clock = clock;
    }

    public async Task<Result<TallySubmissionResponse>> ExecuteAsync(
        ProcessTallySubmissionCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Submission;
        if (string.IsNullOrWhiteSpace(request.ExternalSubmissionId))
        {
            return Result<TallySubmissionResponse>.Failure("intake.external-submission-id.required", "ExternalSubmissionId is required for idempotent webhook processing.");
        }

        if (string.IsNullOrWhiteSpace(request.BusinessName))
        {
            return Result<TallySubmissionResponse>.Failure("intake.business-name.required", "Business name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryContactName))
        {
            return Result<TallySubmissionResponse>.Failure("intake.contact-name.required", "Primary contact name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryContactEmail))
        {
            return Result<TallySubmissionResponse>.Failure("intake.contact-email.required", "Primary contact email is required.");
        }

        if (!IsValidEmail(request.PrimaryContactEmail))
        {
            return Result<TallySubmissionResponse>.Failure("intake.contact-email.invalid", "Primary contact email is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Niche))
        {
            return Result<TallySubmissionResponse>.Failure("intake.niche.required", "Niche is required.");
        }

        if (!IsSupportedLanguage(request.ContentLanguage))
        {
            return Result<TallySubmissionResponse>.Failure("intake.language.invalid", "Content language must be English, Spanish, or Bilingual.");
        }

        if (!string.IsNullOrWhiteSpace(request.WebsiteUrl) && !IsValidHttpUrl(request.WebsiteUrl))
        {
            return Result<TallySubmissionResponse>.Failure("intake.website-url.invalid", "Website URL must be a valid http or https URL.");
        }

        var externalSubmissionId = request.ExternalSubmissionId.Trim();
        var existingReceipt = await _tallySubmissionReceiptRepository.FindByExternalSubmissionIdAsync(externalSubmissionId, cancellationToken);
        if (existingReceipt is not null)
        {
            return Result<TallySubmissionResponse>.Success(ToResponse(existingReceipt));
        }

        var platformLinks = ResolveIncomingLinks(request);
        var invalidLink = platformLinks.FirstOrDefault(link => !IsValidPlatformOrUrl(link));
        if (!string.IsNullOrWhiteSpace(invalidLink))
        {
            return Result<TallySubmissionResponse>.Failure("intake.platform-url.invalid", $"Platform or booking entry is invalid: {invalidLink}");
        }

        var publishingPlatforms = ResolvePublishingPlatforms(platformLinks);
        var calendlyUrl = ResolveCalendlyUrl(request, platformLinks);
        var offer = FirstNonEmpty(request.MainOffer, request.Offer);
        var targetAudience = FirstNonEmpty(request.IdealClientDescription, request.TargetAudience);
        var desiredAction = request.DesiredAction;
        var brandTone = FirstNonEmpty(request.BrandTonePreference, request.BrandTone);
        var painPoints = ResolveList(request.PainPoints, request.PainPointsText);
        var objections = ResolveList(request.Objections, request.ObjectionsText);
        var avoidTopics = ResolveList(request.AvoidTopics, request.AvoidTopicsText);
        var callToActionKeyword = ResolveCallToActionKeyword(request.CallToActionKeyword, desiredAction, request.MainGoal);

        var profile = new ClientProfile(
                request.BusinessName,
                request.PrimaryContactName,
                request.PrimaryContactEmail,
                request.Niche,
                offer,
                targetAudience,
                brandTone,
                callToActionKeyword,
                publishingPlatforms,
                painPoints,
                objections,
                avoidTopics,
                request.PrimaryContactPhone,
                platformLinks,
                calendlyUrl,
                request.WebsiteUrl,
                request.MainGoal,
                desiredAction,
                request.ContentLanguage)
            .Normalize();

        var slug = Slugify(profile.BusinessName);
        var tenantId = new TenantId(BuildStableId("tenant", externalSubmissionId));
        var tenant = Tenant.Create(tenantId, slug, profile, _clock.UtcNow);

        var strategyPlan = new StrategyPlan(
            BuildStableId("strategy", externalSubmissionId),
            tenantId,
            BuildStrategicNarrative(profile),
            BuildPillars(profile),
            1,
            3,
            _clock.UtcNow);

        var windowDays = Math.Clamp(request.BacklogWindowDays, 7, 28);
        var backlog = new EditorialBacklog(
            BuildStableId("backlog", externalSubmissionId),
            tenantId,
            windowDays,
            _clock.UtcNow,
            BuildBacklog(profile, windowDays));

        await _tenantRepository.SaveAsync(tenant, cancellationToken);
        await _strategyPlanRepository.SaveAsync(strategyPlan, cancellationToken);
        await _editorialBacklogRepository.SaveAsync(backlog, cancellationToken);

        var response = new TallySubmissionResponse(
            tenantId.Value,
            slug,
            strategyPlan.StrategyPlanId,
            backlog.EditorialBacklogId,
            backlog.Items.Count);

        await _tallySubmissionReceiptRepository.SaveAsync(
            new TallySubmissionReceipt(
                externalSubmissionId,
                response.TenantId,
                response.Slug,
                response.StrategyPlanId,
                response.EditorialBacklogId,
                response.BacklogItemCount,
                _clock.UtcNow),
            cancellationToken);

        return Result<TallySubmissionResponse>.Success(response);
    }

    private static IReadOnlyList<string> BuildPillars(ClientProfile profile)
    {
        var audience = CleanStrategicValue(profile.TargetAudience);
        var offer = CleanStrategicValue(profile.Offer);
        var niche = CleanStrategicValue(profile.Niche);
        var primaryPain = profile.PainPoints.FirstOrDefault() ?? "low visibility";
        var secondaryPain = profile.PainPoints.Skip(1).FirstOrDefault();
        var primaryObjection = profile.Objections.FirstOrDefault() ?? "uncertainty about the next step";

        var pillars = new List<string>
        {
            $"{offer} strategy for {audience} in {niche}",
            $"How {audience} can solve {NormalizeTopicFragment(primaryPain)}",
            $"How {audience} can overcome {NormalizeTopicFragment(primaryObjection)}",
            $"Proof, use cases, and client outcomes around {offer}",
            BuildConversionPillar(profile)
        };

        if (!string.IsNullOrWhiteSpace(secondaryPain))
        {
            pillars.Insert(2, $"How {audience} can solve {NormalizeTopicFragment(secondaryPain)}");
        }

        return pillars
            .Select(static pillar => pillar.Trim())
            .Where(static pillar => !string.IsNullOrWhiteSpace(pillar))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
    }

    private static IReadOnlyList<EditorialBacklogItem> BuildBacklog(ClientProfile profile, int windowDays)
    {
        var items = new List<EditorialBacklogItem>(windowDays);
        var leadGoal = ResolveLeadGoal(profile);
        for (var day = 0; day < windowDays; day++)
        {
            var category = CategoryRotation[day % CategoryRotation.Length];
            var primaryFormat = (day + 1) % 3 == 0 ? PrimaryFormat.ShortVideo : PrimaryFormat.BrandedGraphic;
            var anchor = ResolveAnchor(profile, category, day);
            items.Add(
                new EditorialBacklogItem(
                    day + 1,
                    day,
                    category,
                    primaryFormat,
                    anchor.Topic,
                    anchor.Angle,
                    anchor.HookDirection,
                    leadGoal,
                    UsesKeywordDrivenCallToAction(profile, category)));
        }

        return items;
    }

    private static (string Topic, string Angle, string HookDirection) ResolveAnchor(ClientProfile profile, ContentCategory category, int index)
    {
        var painPoint = profile.PainPoints[index % profile.PainPoints.Count];
        var objection = profile.Objections[index % profile.Objections.Count];
        var audience = CleanStrategicValue(profile.TargetAudience);
        var offer = CleanStrategicValue(profile.Offer);
        var businessName = CleanStrategicValue(profile.BusinessName);
        var niche = CleanStrategicValue(profile.Niche);

        var normalizedPain = NormalizeTopicFragment(painPoint);
        var normalizedObjection = NormalizeTopicFragment(objection);
        var normalizedGoal = NormalizeTopicFragment(ResolvePrimaryOutcome(profile));
        var conversionDestination = ResolveConversionDestination(profile);

        return category switch
        {
            ContentCategory.PainPoint => (
                $"Why {normalizedPain} keeps {audience} from {normalizedGoal}",
                $"Expose the business cost of leaving {normalizedPain} unresolved and show the first practical shift toward {offer}",
                "Open by naming the pain the audience already feels"),
            ContentCategory.Mistake => (
                $"Mistakes {audience} make when trying to {normalizedGoal}",
                $"Show the avoidable mistake that makes {normalizedGoal} harder and reframe the better path through {offer}",
                "Lead with the most expensive mistake first"),
            ContentCategory.MythBusting => (
                $"Myths about {offer} that keep {audience} stuck",
                $"Challenge the false belief that blocks progress toward {normalizedGoal}",
                "Break a widely believed myth in the opening line"),
            ContentCategory.Faq => (
                $"What buyers ask before investing in {offer}",
                $"Answer a high-friction question in a simple, confidence-building way so the audience can move toward {conversionDestination}",
                "Use a question-based hook that sounds like the buyer's inner dialogue"),
            ContentCategory.ObjectionHandling => (
                $"How to handle '{normalizedObjection}' before choosing {offer}",
                $"Reframe the objection without pressure and show what the audience gains by moving forward",
                "Address the objection directly and empathetically"),
            ContentCategory.Authority => (
                $"{niche} insights that improve {normalizedGoal}",
                $"Teach a nuanced insight that positions {businessName} as the trusted guide for {audience}",
                "Reveal the expert perspective most buyers miss"),
            ContentCategory.Story => (
                $"A client-style story about moving from {normalizedPain} to {normalizedGoal}",
                $"Use a believable before-and-after scenario that makes the transformation feel attainable through {offer}",
                "Open with a vivid scenario the audience can see themselves in"),
            ContentCategory.Comparison => (
                $"{offer} versus patchwork solutions for {audience}",
                $"Contrast the reactive path with the more strategic path and make the tradeoff clear",
                "Use a side-by-side contrast hook"),
            ContentCategory.CtaDriven => (
                BuildCallToActionTopic(profile),
                BuildCallToActionAngle(profile),
                BuildCallToActionHook(profile)),
            _ => (
                $"What delaying {offer} costs {audience}",
                $"Create urgency by showing how waiting makes {normalizedPain} and {normalizedGoal} harder to improve",
                "Lead with the cost of waiting, not generic urgency")
        };
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", "-");
        normalized = Regex.Replace(normalized, @"-+", "-");
        return normalized.Trim('-');
    }

    private static string BuildStableId(string prefix, string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed.Trim().ToLowerInvariant()));
        return $"{prefix}_{Convert.ToHexString(hash)[..24].ToLowerInvariant()}";
    }

    private static string BuildConversionPillar(ClientProfile profile)
    {
        var audience = CleanStrategicValue(profile.TargetAudience);

        return ResolveConversionMode(profile) switch
        {
            ConversionMode.Booking => $"Conversion content that moves {audience} toward booked consultations",
            ConversionMode.Website => $"Conversion content that moves {audience} toward high-intent website visits",
            ConversionMode.DirectMessage => $"Conversion content that moves {audience} into direct-message conversations",
            ConversionMode.CommentKeyword => $"Conversion content that moves {audience} into comment-driven lead capture",
            _ => $"Conversion content aligned to {NormalizeTopicFragment(ResolvePrimaryOutcome(profile))}"
        };
    }

    private static string BuildStrategicNarrative(ClientProfile profile)
    {
        var primaryPain = NormalizeTopicFragment(profile.PainPoints.FirstOrDefault() ?? "low visibility");
        var primaryObjection = NormalizeTopicFragment(profile.Objections.FirstOrDefault() ?? "uncertainty about the next step");
        var conversionDestination = ResolveConversionDestination(profile);
        var audience = CleanStrategicValue(profile.TargetAudience).ToLowerInvariant();
        var offer = CleanStrategicValue(profile.Offer).ToLowerInvariant();

        return
            $"{CleanStrategicValue(profile.BusinessName)} should publish daily {profile.ContentLanguage.ToLowerInvariant()} content for {audience} that positions {offer} as the trusted answer to {primaryPain}, dismantles objections like {primaryObjection}, and consistently moves the audience toward {conversionDestination}.";
    }

    private static string ResolvePrimaryOutcome(ClientProfile profile) =>
        FirstNonEmpty(profile.MainGoal, profile.DesiredAction, $"better results in {profile.Niche}");

    private static string ResolveConversionDestination(ClientProfile profile)
    {
        return ResolveConversionMode(profile) switch
        {
            ConversionMode.Booking => !string.IsNullOrWhiteSpace(profile.CalendlyUrl)
                ? "booking a consultation through the scheduling link"
                : "booking a consultation",
            ConversionMode.Website => !string.IsNullOrWhiteSpace(profile.WebsiteUrl)
                ? "visiting the website and taking the next step there"
                : "visiting the website",
            ConversionMode.DirectMessage => "starting a direct-message conversation",
            ConversionMode.CommentKeyword => $"commenting with the keyword {profile.CallToActionKeyword}",
            _ => NormalizeTopicFragment(ResolvePrimaryOutcome(profile))
        };
    }

    private static string ResolveLeadGoal(ClientProfile profile)
    {
        return ResolveConversionMode(profile) switch
        {
            ConversionMode.Booking => "book_consultation",
            ConversionMode.Website => "visit_website",
            ConversionMode.DirectMessage => "send_dm",
            ConversionMode.CommentKeyword => "comment_keyword",
            _ => "generate_lead"
        };
    }

    private static bool UsesKeywordDrivenCallToAction(ClientProfile profile, ContentCategory category)
    {
        var leadGoal = ResolveLeadGoal(profile);
        if (leadGoal is not ("send_dm" or "comment_keyword"))
        {
            return false;
        }

        return category is ContentCategory.CtaDriven or ContentCategory.ObjectionHandling or ContentCategory.Urgency;
    }

    private static string BuildCallToActionTopic(ClientProfile profile)
    {
        var offer = CleanStrategicValue(profile.Offer);
        var audience = CleanStrategicValue(profile.TargetAudience);

        return ResolveConversionMode(profile) switch
        {
            ConversionMode.Booking => $"What to expect before booking {offer}",
            ConversionMode.Website => $"What {audience} should review before visiting the website",
            ConversionMode.DirectMessage => $"When to send a DM about {offer}",
            ConversionMode.CommentKeyword => $"When to comment '{profile.CallToActionKeyword}' for the next step",
            _ => $"How to take the next step toward {NormalizeTopicFragment(ResolvePrimaryOutcome(profile))}"
        };
    }

    private static string BuildCallToActionAngle(ClientProfile profile)
    {
        return ResolveConversionMode(profile) switch
        {
            ConversionMode.Booking => $"Reduce booking friction by showing the audience exactly why and when to schedule with {profile.BusinessName}",
            ConversionMode.Website => "Build enough curiosity and trust to send the audience to the website with clear purchase intent",
            ConversionMode.DirectMessage => "Lower the barrier to conversation and make the DM feel like the natural next step",
            ConversionMode.CommentKeyword => $"Create curiosity around the keyword {profile.CallToActionKeyword} without sounding gimmicky",
            _ => $"Move the audience toward {NormalizeTopicFragment(ResolvePrimaryOutcome(profile))} with a clear next step"
        };
    }

    private static string BuildCallToActionHook(ClientProfile profile)
    {
        return ResolveConversionMode(profile) switch
        {
            ConversionMode.Booking => "Open by removing the fear or uncertainty behind booking",
            ConversionMode.Website => "Open with a curiosity gap that makes the website visit feel valuable",
            ConversionMode.DirectMessage => "Open by making the conversation feel easy and low-pressure",
            ConversionMode.CommentKeyword => "Open with a CTA-oriented curiosity gap",
            _ => "Open with the clearest next step available"
        };
    }

    private static ConversionMode ResolveConversionMode(ClientProfile profile)
    {
        var desiredAction = profile.DesiredAction;

        if (ContainsAny(desiredAction, "comment"))
        {
            return ConversionMode.CommentKeyword;
        }

        if (ContainsAny(desiredAction, "dm", "message"))
        {
            return ConversionMode.DirectMessage;
        }

        if (ContainsAny(desiredAction, "website", "site", "web", "page", "landing", "visit"))
        {
            return ConversionMode.Website;
        }

        if (ContainsAny(desiredAction, "book", "consult", "call", "appointment"))
        {
            return ConversionMode.Booking;
        }

        if (!string.IsNullOrWhiteSpace(profile.CalendlyUrl))
        {
            return ConversionMode.Booking;
        }

        if (!string.IsNullOrWhiteSpace(profile.WebsiteUrl))
        {
            return ConversionMode.Website;
        }

        return ConversionMode.Generic;
    }

    private static TallySubmissionResponse ToResponse(TallySubmissionReceipt receipt) =>
        new(
            receipt.TenantId,
            receipt.Slug,
            receipt.StrategyPlanId,
            receipt.EditorialBacklogId,
            receipt.BacklogItemCount);

    private static IReadOnlyList<string> ResolveIncomingLinks(TallySubmissionRequest request)
    {
        var links = new List<string>();

        AddIfPresent(links, request.InstagramUrl);
        AddIfPresent(links, request.FacebookPageUrl);
        AddIfPresent(links, request.TikTokUrl);
        AddIfPresent(links, request.LinkedInUrl);
        AddIfPresent(links, request.YouTubeShortsUrl);
        AddIfPresent(links, request.CalendlyUrl);

        if (request.Platforms is not null)
        {
            foreach (var platform in request.Platforms)
            {
                AddIfPresent(links, platform);
            }
        }

        return links
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolvePublishingPlatforms(IReadOnlyList<string> links)
    {
        var resolved = new List<string>();

        foreach (var link in links)
        {
            var normalized = link.Trim().ToLowerInvariant();

            if (normalized.Contains("instagram", StringComparison.Ordinal))
            {
                resolved.Add("Instagram");
            }
            else if (normalized.Contains("facebook", StringComparison.Ordinal))
            {
                resolved.Add("Facebook");
            }
            else if (normalized.Contains("linkedin", StringComparison.Ordinal))
            {
                resolved.Add("LinkedIn");
            }
            else if (normalized.Contains("tiktok", StringComparison.Ordinal))
            {
                resolved.Add("TikTok");
            }
            else if (normalized.Contains("youtube", StringComparison.Ordinal))
            {
                resolved.Add("YouTube");
            }
            else if (normalized.Equals("instagram", StringComparison.Ordinal) ||
                     normalized.Equals("facebook", StringComparison.Ordinal) ||
                     normalized.Equals("linkedin", StringComparison.Ordinal) ||
                     normalized.Equals("tiktok", StringComparison.Ordinal) ||
                     normalized.Equals("youtube", StringComparison.Ordinal))
            {
                resolved.Add(link.Trim());
            }
            else if (!normalized.Contains("calendly", StringComparison.Ordinal) &&
                     !normalized.Contains("calendar.google", StringComparison.Ordinal))
            {
                resolved.Add(link);
            }
        }

        return resolved
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveCalendlyUrl(TallySubmissionRequest request, IReadOnlyList<string> links) =>
        FirstNonEmpty(
            request.CalendlyUrl,
            links.FirstOrDefault(link =>
                link.Contains("calendly", StringComparison.OrdinalIgnoreCase) ||
                link.Contains("calendar.google", StringComparison.OrdinalIgnoreCase)));

    private static IReadOnlyList<string> ResolveList(IReadOnlyList<string>? structuredValues, string? freeformValue)
    {
        var structured = structuredValues?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (structured is { Length: > 0 })
        {
            return structured;
        }

        return (freeformValue ?? string.Empty)
            .Split(['\n', '\r', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.TrimStart('-', '•', '*').Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveCallToActionKeyword(string? explicitKeyword, string? desiredAction, string? mainGoal)
    {
        var provided = FirstNonEmpty(explicitKeyword);
        if (!string.IsNullOrWhiteSpace(provided))
        {
            return provided!;
        }

        var corpus = $"{desiredAction} {mainGoal}".ToLowerInvariant();

        if (corpus.Contains("book", StringComparison.Ordinal) || corpus.Contains("call", StringComparison.Ordinal) || corpus.Contains("appointment", StringComparison.Ordinal))
        {
            return "BOOK";
        }

        if (corpus.Contains("quote", StringComparison.Ordinal) || corpus.Contains("estimate", StringComparison.Ordinal))
        {
            return "QUOTE";
        }

        if (corpus.Contains("message", StringComparison.Ordinal) || corpus.Contains("dm", StringComparison.Ordinal))
        {
            return "DM";
        }

        return "BOOK";
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsValidPlatformOrUrl(string value)
    {
        if (IsValidHttpUrl(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "instagram" or "facebook" or "linkedin" or "tiktok" or "youtube";
    }

    private static bool IsSupportedLanguage(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals("English", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Spanish", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Bilingual", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string? value, params string[] fragments)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeTopicFragment(string value) =>
        value
            .Trim()
            .TrimEnd('.', '!', '?')
            .ToLowerInvariant();

    private static string CleanStrategicValue(string value) =>
        Regex.Replace(value.Trim().TrimEnd('.', '!', '?'), @"\s+", " ");

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static void AddIfPresent(ICollection<string> target, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(value.Trim());
        }
    }
}
