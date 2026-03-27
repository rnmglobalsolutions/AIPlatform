using System.Text.RegularExpressions;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Strategy;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Intake;
using AIMultiAgentPlatform.Domain.Strategy;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Intake;

public sealed class ProcessTallySubmissionUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITallySubmissionReceiptRepository _tallySubmissionReceiptRepository;
    private readonly IStrategyPlanRepository _strategyPlanRepository;
    private readonly IEditorialBacklogRepository _editorialBacklogRepository;
    private readonly IClock _clock;
    private readonly BuildStrategicProfileUseCase _buildStrategicProfileUseCase;
    private readonly GenerateStrategyBlueprintUseCase _generateStrategyBlueprintUseCase;

    public ProcessTallySubmissionUseCase(
        ITenantRepository tenantRepository,
        ITallySubmissionReceiptRepository tallySubmissionReceiptRepository,
        IStrategyPlanRepository strategyPlanRepository,
        IEditorialBacklogRepository editorialBacklogRepository,
        IClock clock,
        BuildStrategicProfileUseCase? buildStrategicProfileUseCase = null,
        GenerateStrategyBlueprintUseCase? generateStrategyBlueprintUseCase = null)
    {
        _tenantRepository = tenantRepository;
        _tallySubmissionReceiptRepository = tallySubmissionReceiptRepository;
        _strategyPlanRepository = strategyPlanRepository;
        _editorialBacklogRepository = editorialBacklogRepository;
        _clock = clock;
        _buildStrategicProfileUseCase = buildStrategicProfileUseCase ?? new BuildStrategicProfileUseCase();
        _generateStrategyBlueprintUseCase = generateStrategyBlueprintUseCase ?? new GenerateStrategyBlueprintUseCase();
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

        if (!IsSupportedContentPlanTier(request.ContentPlanTier))
        {
            return Result<TallySubmissionResponse>.Failure("intake.content-plan-tier.invalid", "Content plan tier must be Starter, Growth, or Premium.");
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
                request.ContentLanguage,
                request.ContentPlanTier)
            .Normalize();

        var slug = Slugify(profile.BusinessName);
        var tenantId = new TenantId(BuildStableId("tenant", externalSubmissionId));
        var tenant = Tenant.Create(tenantId, slug, profile, _clock.UtcNow);
        var strategicProfile = _buildStrategicProfileUseCase.Execute(profile);

        var windowDays = Math.Clamp(request.BacklogWindowDays, 7, 28);
        var strategyBlueprint = await _generateStrategyBlueprintUseCase.ExecuteAsync(strategicProfile, windowDays, cancellationToken);

        var strategyPlan = new StrategyPlan(
            BuildStableId("strategy", externalSubmissionId),
            tenantId,
            strategyBlueprint.StrategicNarrative,
            strategyBlueprint.ContentPillars,
            strategyBlueprint.DailyPostingCadenceDays,
            strategyBlueprint.VideoCadenceDays,
            _clock.UtcNow,
            strategyBlueprint.ContentPlanTier,
            strategyBlueprint.MonthlyVideoTarget);

        var backlog = new EditorialBacklog(
            BuildStableId("backlog", externalSubmissionId),
            tenantId,
            windowDays,
            _clock.UtcNow,
            strategyBlueprint.BacklogBlueprintItems
                .Select(item => new EditorialBacklogItem(
                    item.Sequence,
                    item.PlannedOffsetDays,
                    item.Category,
                    item.PrimaryFormat,
                    item.Topic,
                    item.Angle,
                    item.HookDirection,
                    item.LeadGoal,
                    item.UsesCallToActionKeyword))
                .ToArray());

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

    private static bool IsSupportedContentPlanTier(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals("Starter", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Growth", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Premium", StringComparison.OrdinalIgnoreCase);

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
