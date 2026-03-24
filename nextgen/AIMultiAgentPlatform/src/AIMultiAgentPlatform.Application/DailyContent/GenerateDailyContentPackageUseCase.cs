using System.Text.RegularExpressions;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
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
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public GenerateDailyContentPackageUseCase(
        ITenantRepository tenantRepository,
        IEditorialBacklogRepository editorialBacklogRepository,
        IDailyContentRequestRepository dailyContentRequestRepository,
        IDailyContentBriefRepository dailyContentBriefRepository,
        IPrimaryAssetRepository primaryAssetRepository,
        ICaptionAssetRepository captionAssetRepository,
        IRepurposedAssetBundleRepository repurposedAssetBundleRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _editorialBacklogRepository = editorialBacklogRepository;
        _dailyContentRequestRepository = dailyContentRequestRepository;
        _dailyContentBriefRepository = dailyContentBriefRepository;
        _primaryAssetRepository = primaryAssetRepository;
        _captionAssetRepository = captionAssetRepository;
        _repurposedAssetBundleRepository = repurposedAssetBundleRepository;
        _idGenerator = idGenerator;
        _clock = clock;
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

        var brief = BuildBrief(dailyRequest, tenant.Profile, backlogItem);
        var primaryAsset = BuildPrimaryAsset(dailyRequest, brief, tenant.Profile);
        var captionAsset = BuildCaptionAsset(dailyRequest, brief, primaryAsset, tenant.Profile);
        var repurposedAssetBundle = BuildRepurposedAssetBundle(dailyRequest, brief, primaryAsset, tenant.Profile);

        await _dailyContentRequestRepository.SaveAsync(dailyRequest, cancellationToken);
        await _dailyContentBriefRepository.SaveAsync(brief, cancellationToken);
        await _primaryAssetRepository.SaveAsync(primaryAsset, cancellationToken);
        await _captionAssetRepository.SaveAsync(captionAsset, cancellationToken);
        await _repurposedAssetBundleRepository.SaveAsync(repurposedAssetBundle, cancellationToken);

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
        EditorialBacklogItem backlogItem)
    {
        var coreMessage =
            $"{backlogItem.Angle}. Show {profile.TargetAudience.ToLowerInvariant()} how {profile.Offer.ToLowerInvariant()} helps them move past {backlogItem.Topic.ToLowerInvariant()} without sounding repetitive.";

        return new DailyContentBrief(
            _idGenerator.NewId("brief"),
            request.DailyContentRequestId,
            request.TenantId,
            backlogItem.Category,
            backlogItem.PrimaryFormat,
            backlogItem.Topic,
            backlogItem.Angle,
            backlogItem.HookDirection,
            coreMessage,
            profile.CallToActionKeyword,
            profile.BrandTone);
    }

    private PrimaryAsset BuildPrimaryAsset(
        DailyContentRequest request,
        DailyContentBrief brief,
        ClientProfile profile)
    {
        var headline = brief.PrimaryFormat == PrimaryFormat.ShortVideo
            ? $"Short video: {brief.Topic}"
            : $"Graphic post: {brief.Topic}";

        var hook = $"{brief.HookDirection}. {brief.Topic}.";
        var body = brief.PrimaryFormat == PrimaryFormat.ShortVideo
            ? $"HOOK: {hook}\nBODY: Teach one practical shift around {brief.Angle.ToLowerInvariant()} for {profile.TargetAudience.ToLowerInvariant()}.\nPAYOFF: Tie the lesson back to {profile.Offer.ToLowerInvariant()} with a clear next step."
            : $"Lead with a bold headline about {brief.Topic}. Reinforce {brief.Angle.ToLowerInvariant()} in concise supporting copy for a Canva-ready branded graphic.";
        var payoff = brief.PrimaryFormat == PrimaryFormat.ShortVideo
            ? $"Leave the viewer with one simple action they can take today to improve {profile.Niche.ToLowerInvariant()} performance."
            : $"Make the visual feel actionable and save-worthy so the audience wants to revisit the message later.";
        var callToAction = $"Invite the audience to comment or DM '{brief.CallToActionKeyword}'.";
        var productionNotes = brief.PrimaryFormat == PrimaryFormat.ShortVideo
            ? "15-45 second HeyGen-compatible script. Keep cadence natural, conversational, and easy to subtitle."
            : "Design in Canva with strong brand hierarchy, one core message, and a CTA-ready footer treatment.";

        return new PrimaryAsset(
            _idGenerator.NewId("primary_asset"),
            request.DailyContentRequestId,
            request.TenantId,
            brief.PrimaryFormat,
            headline,
            hook,
            body,
            payoff,
            callToAction,
            productionNotes);
    }

    private CaptionAsset BuildCaptionAsset(
        DailyContentRequest request,
        DailyContentBrief brief,
        PrimaryAsset primaryAsset,
        ClientProfile profile)
    {
        var engagementPrompt = $"Ask the audience what part of {brief.Topic.ToLowerInvariant()} is slowing them down most.";
        var caption =
            $"{primaryAsset.Hook} {brief.Angle}. {brief.CoreMessage} {engagementPrompt} Comment or DM '{brief.CallToActionKeyword}' to keep the conversation going.";

        return new CaptionAsset(
            _idGenerator.NewId("caption"),
            request.DailyContentRequestId,
            caption,
            engagementPrompt,
            brief.CallToActionKeyword,
            BuildHashtags(profile));
    }

    private RepurposedAssetBundle BuildRepurposedAssetBundle(
        DailyContentRequest request,
        DailyContentBrief brief,
        PrimaryAsset primaryAsset,
        ClientProfile profile)
    {
        var carouselOutline =
            $"Slide 1: {brief.Topic}\nSlide 2: Why this matters\nSlide 3: The common mistake\nSlide 4: The better move\nSlide 5: CTA -> {brief.CallToActionKeyword}";

        var storyFrames = new[]
        {
            $"Frame 1: Quick tension around {brief.Topic}",
            $"Frame 2: One insight on {brief.Angle.ToLowerInvariant()}",
            $"Frame 3: CTA sticker -> {brief.CallToActionKeyword}"
        };

        var linkedInPost =
            $"Most teams don’t have a content problem. They have a positioning problem around {brief.Topic.ToLowerInvariant()}. {brief.Angle}.\n\n{brief.CoreMessage}\n\nIf you want the framework, comment {brief.CallToActionKeyword}.";

        var quotePost =
            $"\"{brief.Angle}. The right content turns attention into conversations and conversations into qualified demand.\"";

        var shortClipIdea =
            $"Create a 10-second clip that isolates the strongest line from the hook: '{primaryAsset.Hook}' and pair it with fast captions plus a CTA for {brief.CallToActionKeyword}.";

        var commentHooks = new[]
        {
            $"What is the biggest blocker you see around {brief.Topic.ToLowerInvariant()}?",
            $"Would a template for this help? Comment {brief.CallToActionKeyword}.",
            $"Which part feels harder right now: consistency or conversion?"
        };

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
