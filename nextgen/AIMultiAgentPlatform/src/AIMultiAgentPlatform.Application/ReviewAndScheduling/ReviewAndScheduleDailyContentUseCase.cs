using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Reviewing;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Reviewing;

namespace AIMultiAgentPlatform.Application.ReviewAndScheduling;

public sealed class ReviewAndScheduleDailyContentUseCase
{
    private static readonly string[] AbsoluteClaims =
    [
        "guarantee",
        "guaranteed",
        "always",
        "never",
        "instant",
        "overnight"
    ];

    private readonly ITenantRepository _tenantRepository;
    private readonly IDailyContentRequestRepository _dailyContentRequestRepository;
    private readonly IDailyContentBriefRepository _dailyContentBriefRepository;
    private readonly IPrimaryAssetRepository _primaryAssetRepository;
    private readonly ICaptionAssetRepository _captionAssetRepository;
    private readonly IRepurposedAssetBundleRepository _repurposedAssetBundleRepository;
    private readonly IComplianceReviewRepository _complianceReviewRepository;
    private readonly IQualityReviewRepository _qualityReviewRepository;
    private readonly IApprovalRequestRepository _approvalRequestRepository;
    private readonly ISchedulingJobRepository _schedulingJobRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly EvaluateGeneratedContentUseCase _evaluateGeneratedContentUseCase;

    public ReviewAndScheduleDailyContentUseCase(
        ITenantRepository tenantRepository,
        IDailyContentRequestRepository dailyContentRequestRepository,
        IDailyContentBriefRepository dailyContentBriefRepository,
        IPrimaryAssetRepository primaryAssetRepository,
        ICaptionAssetRepository captionAssetRepository,
        IRepurposedAssetBundleRepository repurposedAssetBundleRepository,
        IComplianceReviewRepository complianceReviewRepository,
        IQualityReviewRepository qualityReviewRepository,
        IApprovalRequestRepository approvalRequestRepository,
        ISchedulingJobRepository schedulingJobRepository,
        IIdGenerator idGenerator,
        IClock clock,
        EvaluateGeneratedContentUseCase? evaluateGeneratedContentUseCase = null)
    {
        _tenantRepository = tenantRepository;
        _dailyContentRequestRepository = dailyContentRequestRepository;
        _dailyContentBriefRepository = dailyContentBriefRepository;
        _primaryAssetRepository = primaryAssetRepository;
        _captionAssetRepository = captionAssetRepository;
        _repurposedAssetBundleRepository = repurposedAssetBundleRepository;
        _complianceReviewRepository = complianceReviewRepository;
        _qualityReviewRepository = qualityReviewRepository;
        _approvalRequestRepository = approvalRequestRepository;
        _schedulingJobRepository = schedulingJobRepository;
        _idGenerator = idGenerator;
        _clock = clock;
        _evaluateGeneratedContentUseCase = evaluateGeneratedContentUseCase ?? new EvaluateGeneratedContentUseCase();
    }

    public async Task<Result<ReviewAndScheduleDailyContentResponse>> ExecuteAsync(
        ReviewAndScheduleDailyContentCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<ReviewAndScheduleDailyContentResponse>.Failure("review.tenant.required", "TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DailyContentRequestId))
        {
            return Result<ReviewAndScheduleDailyContentResponse>.Failure("review.request.required", "DailyContentRequestId is required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<ReviewAndScheduleDailyContentResponse>.Failure("review.tenant.not-found", "Tenant was not found.");
        }

        var dailyRequest = await _dailyContentRequestRepository.FindByIdAsync(request.DailyContentRequestId, cancellationToken);
        if (dailyRequest is null)
        {
            return Result<ReviewAndScheduleDailyContentResponse>.Failure("review.request.not-found", "Daily content request was not found.");
        }

        if (dailyRequest.TenantId != tenant.TenantId)
        {
            return Result<ReviewAndScheduleDailyContentResponse>.Failure("review.request.mismatch", "Daily content request does not belong to the tenant.");
        }

        var brief = await _dailyContentBriefRepository.FindByRequestIdAsync(dailyRequest.DailyContentRequestId, cancellationToken);
        var primaryAsset = await _primaryAssetRepository.FindByRequestIdAsync(dailyRequest.DailyContentRequestId, cancellationToken);
        var captionAsset = await _captionAssetRepository.FindByRequestIdAsync(dailyRequest.DailyContentRequestId, cancellationToken);
        var repurposedAssetBundle = await _repurposedAssetBundleRepository.FindByRequestIdAsync(dailyRequest.DailyContentRequestId, cancellationToken);

        if (brief is null || primaryAsset is null || captionAsset is null || repurposedAssetBundle is null)
        {
            return Result<ReviewAndScheduleDailyContentResponse>.Failure("review.package.incomplete", "Daily content package is incomplete.");
        }

        var complianceReview = BuildComplianceReview(tenant, dailyRequest, brief, primaryAsset, captionAsset, repurposedAssetBundle);
        var qualityReview = BuildQualityReview(tenant, dailyRequest, brief, primaryAsset, captionAsset, repurposedAssetBundle);
        var approvalRequest = BuildApprovalRequest(tenant, dailyRequest, complianceReview, qualityReview);
        var schedulingJob = BuildSchedulingJob(tenant, dailyRequest, primaryAsset, captionAsset, approvalRequest);

        await _complianceReviewRepository.SaveAsync(complianceReview, cancellationToken);
        await _qualityReviewRepository.SaveAsync(qualityReview, cancellationToken);
        await _approvalRequestRepository.SaveAsync(approvalRequest, cancellationToken);
        await _schedulingJobRepository.SaveAsync(schedulingJob, cancellationToken);

        return Result<ReviewAndScheduleDailyContentResponse>.Success(
            new ReviewAndScheduleDailyContentResponse(
                complianceReview.ComplianceReviewId,
                qualityReview.QualityReviewId,
                approvalRequest.ApprovalRequestId,
                schedulingJob.SchedulingJobId,
                complianceReview.RiskLevel.ToString(),
                qualityReview.OverallScore,
                approvalRequest.Status.ToString(),
                schedulingJob.Status.ToString(),
                schedulingJob.Targets.Count));
    }

    private ComplianceReview BuildComplianceReview(
        Domain.Tenants.Tenant tenant,
        DailyContentRequest request,
        DailyContentBrief brief,
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        RepurposedAssetBundle repurposedAssetBundle)
    {
        var corpus = string.Join(
            "\n",
            brief.CoreMessage,
            primaryAsset.Headline,
            primaryAsset.Hook,
            primaryAsset.Body,
            primaryAsset.Payoff,
            captionAsset.Caption,
            repurposedAssetBundle.LinkedInPost,
            repurposedAssetBundle.QuotePost,
            repurposedAssetBundle.ShortClipIdea);

        var issues = new List<ComplianceIssue>();

        foreach (var topic in tenant.Profile.AvoidTopics)
        {
            if (ContainsIgnoreCase(corpus, topic))
            {
                issues.Add(
                    new ComplianceIssue(
                        "avoid-topic",
                        $"Content references avoided topic '{topic}'.",
                        $"Remove or reframe any mention of '{topic}' to stay within the tenant guardrails."));
            }
        }

        foreach (var claim in AbsoluteClaims)
        {
            if (ContainsIgnoreCase(corpus, claim))
            {
                issues.Add(
                    new ComplianceIssue(
                        "absolute-claim",
                        $"Content contains strong claim language '{claim}'.",
                        "Replace absolutes with softer, evidence-based language."));
            }
        }

        var riskLevel = issues.Any(issue => issue.Code == "avoid-topic")
            ? RiskLevel.High
            : issues.Any()
                ? RiskLevel.Medium
                : RiskLevel.Low;

        var safeVersionSummary = riskLevel switch
        {
            RiskLevel.High => "Remove blocked topics before the content can move forward.",
            RiskLevel.Medium => "Soften claims and keep the message educational, not absolute.",
            _ => "Content is safe to proceed with the current guardrails."
        };

        return new ComplianceReview(
            _idGenerator.NewId("compliance"),
            request.DailyContentRequestId,
            tenant.TenantId,
            riskLevel,
            issues,
            safeVersionSummary,
            _clock.UtcNow);
    }

    private QualityReview BuildQualityReview(
        Domain.Tenants.Tenant tenant,
        DailyContentRequest request,
        DailyContentBrief brief,
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        RepurposedAssetBundle repurposedAssetBundle)
    {
        var evaluation = _evaluateGeneratedContentUseCase.Execute(
            tenant.Profile,
            brief,
            primaryAsset,
            captionAsset,
            repurposedAssetBundle);

        return new QualityReview(
            _idGenerator.NewId("quality"),
            request.DailyContentRequestId,
            tenant.TenantId,
            evaluation.HookScore,
            evaluation.ClarityScore,
            evaluation.RelevanceScore,
            evaluation.LeadGenerationScore,
            evaluation.OverallScore,
            evaluation.Feedback,
            evaluation.OptimizedCallToAction,
            _clock.UtcNow,
            evaluation.SpecificityScore,
            evaluation.PlatformFitScore,
            evaluation.AntiRepetitionScore,
            evaluation.Warnings);
    }

    private ApprovalRequest BuildApprovalRequest(
        Domain.Tenants.Tenant tenant,
        DailyContentRequest request,
        ComplianceReview complianceReview,
        QualityReview qualityReview)
    {
        var status = complianceReview.RiskLevel == RiskLevel.High || qualityReview.OverallScore < 7.5
            ? ApprovalStatus.NeedsChanges
            : ApprovalStatus.Approved;

        var summary = status == ApprovalStatus.Approved
            ? "Content passed automated compliance and quality review and is ready for scheduling."
            : "Content needs changes before scheduling due to compliance or quality thresholds.";

        return new ApprovalRequest(
            _idGenerator.NewId("approval"),
            request.DailyContentRequestId,
            tenant.TenantId,
            status,
            summary,
            _clock.UtcNow);
    }

    private SchedulingJob BuildSchedulingJob(
        Domain.Tenants.Tenant tenant,
        DailyContentRequest request,
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        ApprovalRequest approvalRequest)
    {
        if (approvalRequest.Status != ApprovalStatus.Approved)
        {
            return new SchedulingJob(
                _idGenerator.NewId("schedule"),
                request.DailyContentRequestId,
                tenant.TenantId,
                SchedulingStatus.Blocked,
                approvalRequest.DecisionSummary,
                _clock.UtcNow,
                Array.Empty<PublicationTarget>());
        }

        var scheduledUtc = NextPublicationWindowUtc(_clock.UtcNow);
        var targets = tenant.Profile.Platforms
            .Select(platform => new PublicationTarget(
                platform,
                scheduledUtc,
                $"{primaryAsset.PrimaryFormat}: {primaryAsset.Headline} | CTA: {ResolveTargetCallToAction(tenant.Profile, captionAsset.CallToActionKeyword)} | Language: {tenant.Profile.ContentLanguage}"))
            .ToArray();

        return new SchedulingJob(
            _idGenerator.NewId("schedule"),
            request.DailyContentRequestId,
            tenant.TenantId,
            SchedulingStatus.Scheduled,
            "Content auto-approved and scheduled on all linked platforms.",
            _clock.UtcNow,
            targets);
    }

    private static DateTime NextPublicationWindowUtc(DateTime utcNow) =>
        new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 14, 0, 0, DateTimeKind.Utc).AddDays(1);

    private static bool ContainsIgnoreCase(string corpus, string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        corpus.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string ResolveTargetCallToAction(Domain.Tenants.ClientProfile profile, string callToActionKeyword) =>
        RequiresBookingCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.CalendlyUrl)
            ? $"Book via {profile.CalendlyUrl}"
            : RequiresWebsiteCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.WebsiteUrl)
                ? $"Visit {profile.WebsiteUrl}"
            : callToActionKeyword;

    private static bool RequiresBookingCallToAction(Domain.Tenants.ClientProfile profile) =>
        ContainsIgnoreCase(profile.DesiredAction, "book") ||
        ContainsIgnoreCase(profile.DesiredAction, "consult") ||
        ContainsIgnoreCase(profile.DesiredAction, "appointment") ||
        ContainsIgnoreCase(profile.DesiredAction, "call");

    private static bool RequiresWebsiteCallToAction(Domain.Tenants.ClientProfile profile) =>
        ContainsIgnoreCase(profile.DesiredAction, "website") ||
        ContainsIgnoreCase(profile.DesiredAction, "site") ||
        ContainsIgnoreCase(profile.DesiredAction, "web") ||
        ContainsIgnoreCase(profile.DesiredAction, "page") ||
        ContainsIgnoreCase(profile.DesiredAction, "landing") ||
        ContainsIgnoreCase(profile.DesiredAction, "visit");
}
