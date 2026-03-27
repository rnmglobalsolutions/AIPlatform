using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Content;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.DailyContent;

public sealed class GenerateDailyContentPackageUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IEditorialBacklogRepository _editorialBacklogRepository;
    private readonly IDailyContentRequestRepository _dailyContentRequestRepository;
    private readonly IDailyContentBriefRepository _dailyContentBriefRepository;
    private readonly IPrimaryAssetRepository _primaryAssetRepository;
    private readonly ICaptionAssetRepository _captionAssetRepository;
    private readonly IRepurposedAssetBundleRepository _repurposedAssetBundleRepository;
    private readonly IContentMemoryRepository? _contentMemoryRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly GenerateCanonicalContentFrameUseCase _generateCanonicalContentFrameUseCase;

    public GenerateDailyContentPackageUseCase(
        ITenantRepository tenantRepository,
        IEditorialBacklogRepository editorialBacklogRepository,
        IDailyContentRequestRepository dailyContentRequestRepository,
        IDailyContentBriefRepository dailyContentBriefRepository,
        IPrimaryAssetRepository primaryAssetRepository,
        ICaptionAssetRepository captionAssetRepository,
        IRepurposedAssetBundleRepository repurposedAssetBundleRepository,
        IIdGenerator idGenerator,
        IClock clock,
        IContentMemoryRepository? contentMemoryRepository = null,
        GenerateCanonicalContentFrameUseCase? generateCanonicalContentFrameUseCase = null)
    {
        _tenantRepository = tenantRepository;
        _editorialBacklogRepository = editorialBacklogRepository;
        _dailyContentRequestRepository = dailyContentRequestRepository;
        _dailyContentBriefRepository = dailyContentBriefRepository;
        _primaryAssetRepository = primaryAssetRepository;
        _captionAssetRepository = captionAssetRepository;
        _repurposedAssetBundleRepository = repurposedAssetBundleRepository;
        _contentMemoryRepository = contentMemoryRepository;
        _idGenerator = idGenerator;
        _clock = clock;
        _generateCanonicalContentFrameUseCase = generateCanonicalContentFrameUseCase ?? new GenerateCanonicalContentFrameUseCase();
    }

    public async Task<Result<GenerateDailyContentPackageResponse>> ExecuteAsync(
        GenerateDailyContentPackageCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<GenerateDailyContentPackageResponse>.Failure("daily-content.tenant.required", "TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.EditorialBacklogId))
        {
            return Result<GenerateDailyContentPackageResponse>.Failure("daily-content.backlog.required", "EditorialBacklogId is required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<GenerateDailyContentPackageResponse>.Failure("daily-content.tenant.not-found", "Tenant was not found.");
        }

        var backlog = await _editorialBacklogRepository.FindByIdAsync(request.EditorialBacklogId, cancellationToken);
        if (backlog is null)
        {
            return Result<GenerateDailyContentPackageResponse>.Failure("daily-content.backlog.not-found", "Editorial backlog was not found.");
        }

        if (backlog.TenantId != tenant.TenantId)
        {
            return Result<GenerateDailyContentPackageResponse>.Failure("daily-content.backlog.mismatch", "The backlog does not belong to the tenant.");
        }

        var backlogItem = backlog.Items.SingleOrDefault(item => item.Sequence == request.Sequence);
        if (backlogItem is null)
        {
            return Result<GenerateDailyContentPackageResponse>.Failure("daily-content.sequence.not-found", "The requested backlog sequence was not found.");
        }

        var correlationId = string.IsNullOrWhiteSpace(command.CorrelationId)
            ? $"daily-{tenant.TenantId.Value}-{request.Sequence}"
            : command.CorrelationId.Trim();

        var dailyRequest = new DailyContentRequest(
            _idGenerator.NewId("daily_request"),
            tenant.TenantId,
            backlog.EditorialBacklogId,
            backlogItem.Sequence,
            _clock.UtcNow,
            correlationId);

        var memorySnapshot = _contentMemoryRepository is null
            ? null
            : await _contentMemoryRepository.GetSnapshotAsync(tenant.TenantId.Value, 10, cancellationToken);
        var canonicalFrame = await _generateCanonicalContentFrameUseCase.ExecuteAsync(
            tenant.TenantId,
            tenant.Profile,
            backlogItem,
            memorySnapshot,
            cancellationToken);

        var brief = BuildBrief(dailyRequest, tenant.Profile, canonicalFrame);
        var primaryAsset = BuildPrimaryAsset(dailyRequest, canonicalFrame);
        var captionAsset = BuildCaptionAsset(dailyRequest, canonicalFrame, tenant.Profile);
        var repurposedAssetBundle = BuildRepurposedAssetBundle(dailyRequest, canonicalFrame, tenant.Profile);

        await _dailyContentRequestRepository.SaveAsync(dailyRequest, cancellationToken);
        await _dailyContentBriefRepository.SaveAsync(brief, cancellationToken);
        await _primaryAssetRepository.SaveAsync(primaryAsset, cancellationToken);
        await _captionAssetRepository.SaveAsync(captionAsset, cancellationToken);
        await _repurposedAssetBundleRepository.SaveAsync(repurposedAssetBundle, cancellationToken);
        if (_contentMemoryRepository is not null)
        {
            await _contentMemoryRepository.SaveAsync(
                BuildContentMemoryEntry(dailyRequest, tenant, backlogItem, canonicalFrame, primaryAsset),
                cancellationToken);
        }

        return Result<GenerateDailyContentPackageResponse>.Success(
            new GenerateDailyContentPackageResponse(
                dailyRequest.DailyContentRequestId,
                brief.DailyContentBriefId,
                primaryAsset.PrimaryAssetId,
                captionAsset.CaptionAssetId,
                repurposedAssetBundle.RepurposedAssetBundleId,
                brief.PrimaryFormat.ToString()));
    }

    private DailyContentBrief BuildBrief(
        DailyContentRequest request,
        ClientProfile profile,
        CanonicalContentFrame canonicalFrame) =>
        new(
            _idGenerator.NewId("brief"),
            request.DailyContentRequestId,
            request.TenantId,
            canonicalFrame.Category,
            canonicalFrame.PrimaryFormat,
            canonicalFrame.Topic,
            canonicalFrame.Angle,
            canonicalFrame.HookDirection,
            canonicalFrame.CoreMessage,
            canonicalFrame.CallToActionKeyword,
            profile.BrandTone);

    private PrimaryAsset BuildPrimaryAsset(
        DailyContentRequest request,
        CanonicalContentFrame canonicalFrame)
    {
        var headline = canonicalFrame.PrimaryFormat == PrimaryFormat.ShortVideo
            ? $"Short video: {canonicalFrame.Topic}"
            : $"Graphic post: {canonicalFrame.Topic}";

        return new PrimaryAsset(
            _idGenerator.NewId("primary_asset"),
            request.DailyContentRequestId,
            request.TenantId,
            canonicalFrame.PrimaryFormat,
            headline,
            canonicalFrame.PrimaryHook,
            canonicalFrame.Body,
            canonicalFrame.Payoff,
            canonicalFrame.CallToAction,
            canonicalFrame.ProductionNotes);
    }

    private CaptionAsset BuildCaptionAsset(
        DailyContentRequest request,
        CanonicalContentFrame canonicalFrame,
        ClientProfile profile)
    {
        var caption =
            $"{canonicalFrame.PrimaryHook} {canonicalFrame.Angle}. {canonicalFrame.CoreMessage} {canonicalFrame.EngagementPrompt} {canonicalFrame.DesiredActionPrompt}";

        return new CaptionAsset(
            _idGenerator.NewId("caption"),
            request.DailyContentRequestId,
            caption,
            canonicalFrame.EngagementPrompt,
            canonicalFrame.CallToActionKeyword,
            BuildHashtags(profile));
    }

    private RepurposedAssetBundle BuildRepurposedAssetBundle(
        DailyContentRequest request,
        CanonicalContentFrame canonicalFrame,
        ClientProfile profile)
    {
        var carouselDirective = canonicalFrame.RepurposeDirectives.FirstOrDefault(directive =>
            directive.Format.Equals("Carousel", StringComparison.OrdinalIgnoreCase));
        var storiesDirective = canonicalFrame.RepurposeDirectives.FirstOrDefault(directive =>
            directive.Format.Equals("Stories", StringComparison.OrdinalIgnoreCase));
        var linkedInDirective = canonicalFrame.RepurposeDirectives.FirstOrDefault(directive =>
            directive.Format.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase));
        var repurposeCallToAction = BuildRepurposeCallToAction(profile, canonicalFrame.CallToActionKeyword);
        var carouselOutline =
            $"Slide 1: {canonicalFrame.Topic}\nSlide 2: {canonicalFrame.PrimaryHook}\nSlide 3: {canonicalFrame.Angle}\nSlide 4: {canonicalFrame.Payoff}\nSlide 5: CTA -> {carouselDirective?.Prompt ?? repurposeCallToAction}";

        var storyFrames = new[]
        {
            $"Frame 1: {canonicalFrame.PrimaryHook}",
            $"Frame 2: {storiesDirective?.Intent ?? $"One insight on {canonicalFrame.Angle.ToLowerInvariant()}"}",
            $"Frame 3: CTA sticker -> {storiesDirective?.Prompt ?? repurposeCallToAction}"
        };

        var linkedInPost =
            $"{linkedInDirective?.Intent ?? $"Most teams don’t have a content problem around {canonicalFrame.Topic.ToLowerInvariant()}."}\n\n{canonicalFrame.CoreMessage}\n\n{linkedInDirective?.Prompt ?? canonicalFrame.DesiredActionPrompt}";

        var quotePost =
            $"\"{canonicalFrame.Angle}. {canonicalFrame.Payoff}\"";

        var shortClipIdea =
            $"Create a 10-second clip from the hook '{canonicalFrame.PrimaryHook}' and land on '{canonicalFrame.CallToAction}'.";

        var commentHooks = canonicalFrame.HookVariants
            .Take(2)
            .Select(variant => variant.Text)
            .Append(BuildFollowUpQuestion(profile, canonicalFrame.CallToActionKeyword))
            .ToArray();

        return new RepurposedAssetBundle(
            _idGenerator.NewId("repurpose"),
            request.DailyContentRequestId,
            carouselOutline,
            storyFrames,
            linkedInPost,
            quotePost,
            shortClipIdea,
            commentHooks);
    }

    private ContentMemoryEntry BuildContentMemoryEntry(
        DailyContentRequest request,
        Tenant tenant,
        EditorialBacklogItem backlogItem,
        CanonicalContentFrame canonicalFrame,
        PrimaryAsset primaryAsset)
    {
        var contentHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    $"{canonicalFrame.PrimaryFormat}|{canonicalFrame.Topic}|{canonicalFrame.PrimaryHook}|{canonicalFrame.CallToAction}")))
            .ToLowerInvariant();

        return new ContentMemoryEntry(
            _idGenerator.NewId("content_memory"),
            tenant.TenantId,
            nameof(PrimaryAsset),
            primaryAsset.PrimaryAssetId,
            canonicalFrame.Topic,
            canonicalFrame.PrimaryHook,
            canonicalFrame.CallToAction,
            backlogItem.LeadGoal,
            tenant.Profile.Platforms.FirstOrDefault() ?? "Instagram",
            contentHash,
            _clock.UtcNow,
            ContentMemoryLifecycleStage.Generated,
            SourceBacklogItemId: $"{request.EditorialBacklogId}:{backlogItem.Sequence}",
            SourceStrategyPlanId: request.EditorialBacklogId);
    }

    private static string BuildRepurposeCallToAction(ClientProfile profile, string callToActionKeyword) =>
        RequiresBookingCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.CalendlyUrl)
            ? $"Book via {profile.CalendlyUrl}"
            : RequiresWebsiteCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.WebsiteUrl)
                ? $"Visit {profile.WebsiteUrl}"
            : callToActionKeyword;

    private static string BuildFollowUpQuestion(ClientProfile profile, string callToActionKeyword)
    {
        if (RequiresBookingCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.CalendlyUrl))
        {
            return $"Ready to take action? Book through {profile.CalendlyUrl} or DM '{callToActionKeyword}'.";
        }

        if (RequiresWebsiteCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.WebsiteUrl))
        {
            return $"Ready to take action? Visit {profile.WebsiteUrl} or DM '{callToActionKeyword}' if you want help first.";
        }

        return $"Would a template for this help? Comment {callToActionKeyword}.";
    }

    private static bool RequiresBookingCallToAction(ClientProfile profile) =>
        ContainsAny(profile.DesiredAction, "book", "consult", "call", "appointment");

    private static bool RequiresWebsiteCallToAction(ClientProfile profile) =>
        ContainsAny(profile.DesiredAction, "website", "site", "web", "page", "landing", "visit");

    private static bool ContainsAny(string? value, params string[] fragments)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> BuildHashtags(ClientProfile profile)
    {
        var tokens = new[]
        {
            profile.Niche,
            profile.Offer,
            "Lead Generation",
            "Content Strategy"
        };

        return tokens
            .Select(value => "#" + Regex.Replace(value, @"[^A-Za-z0-9]", string.Empty))
            .Where(value => value.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
