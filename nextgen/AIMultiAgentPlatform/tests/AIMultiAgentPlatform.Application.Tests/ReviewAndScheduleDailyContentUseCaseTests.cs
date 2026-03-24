using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.ReviewAndScheduling;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Reviewing;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class ReviewAndScheduleDailyContentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ApprovesAndSchedulesSafeContent()
    {
        var tenant = CreateTenant(["Instagram", "LinkedIn"], ["Politics"]);
        var request = new DailyContentRequest("daily_request_001", tenant.TenantId, "backlog_001", 3, DateTime.UtcNow, "corr-123");
        var brief = new DailyContentBrief("brief_001", request.DailyContentRequestId, tenant.TenantId, ContentCategory.Authority, PrimaryFormat.ShortVideo, "Authority topic", "Teach the smarter move", "Open with the hidden edge", "Core message around authority", "BOOK", "Bold");
        var primaryAsset = new PrimaryAsset("primary_asset_001", request.DailyContentRequestId, tenant.TenantId, PrimaryFormat.ShortVideo, "Short video: Authority topic", "Open with the hidden edge. Authority topic.", "HOOK: Open with the hidden edge.\nBODY: Teach the smarter move.\nPAYOFF: Tie it back to the offer.", "Give one next step.", "Invite the audience to comment or DM 'BOOK'.", "15-45 second HeyGen-compatible script. Keep cadence natural, conversational, and easy to subtitle.");
        var caption = new CaptionAsset("caption_001", request.DailyContentRequestId, "Open with the hidden edge. Teach the smarter move. Comment or DM 'BOOK'.", "Ask what is slowing them down.", "BOOK", ["#B2BConsultants"]);
        var bundle = new RepurposedAssetBundle("repurpose_001", request.DailyContentRequestId, "Carousel", ["Frame 1", "Frame 2", "Frame 3"], "LinkedIn post", "Quote", "Clip idea", ["Hook 1", "Hook 2"]);

        var useCase = CreateUseCase(
            tenant,
            request,
            brief,
            primaryAsset,
            caption,
            bundle,
            out var complianceRepository,
            out var qualityRepository,
            out var approvalRepository,
            out var schedulingRepository);

        var result = await useCase.ExecuteAsync(
            new ReviewAndScheduleDailyContentCommand(
                new ReviewAndScheduleDailyContentRequest(tenant.TenantId.Value, request.DailyContentRequestId, "corr-123"),
                "corr-123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Low", result.Value!.RiskLevel);
        Assert.Equal("Approved", result.Value.ApprovalStatus);
        Assert.Equal("Scheduled", result.Value.SchedulingStatus);
        Assert.Equal(2, result.Value.ScheduledTargetCount);
        Assert.NotNull(complianceRepository.Saved);
        Assert.True(qualityRepository.Saved!.OverallScore >= 7.5);
        Assert.Equal(ApprovalStatus.Approved, approvalRepository.Saved!.Status);
        Assert.Equal(SchedulingStatus.Scheduled, schedulingRepository.Saved!.Status);
    }

    [Fact]
    public async Task ExecuteAsync_BlocksSchedulingWhenAvoidedTopicDetected()
    {
        var tenant = CreateTenant(["Instagram"], ["Politics"]);
        var request = new DailyContentRequest("daily_request_001", tenant.TenantId, "backlog_001", 1, DateTime.UtcNow, "corr-123");
        var brief = new DailyContentBrief("brief_001", request.DailyContentRequestId, tenant.TenantId, ContentCategory.Story, PrimaryFormat.BrandedGraphic, "Topic", "Angle", "Hook", "Core message", "BOOK", "Bold");
        var primaryAsset = new PrimaryAsset("primary_asset_001", request.DailyContentRequestId, tenant.TenantId, PrimaryFormat.BrandedGraphic, "Headline", "Hook", "This body references politics and should be blocked.", "Payoff", "CTA", "Notes");
        var caption = new CaptionAsset("caption_001", request.DailyContentRequestId, "Caption", "Prompt", "BOOK", ["#Test"]);
        var bundle = new RepurposedAssetBundle("repurpose_001", request.DailyContentRequestId, "Carousel", ["Frame 1"], "LinkedIn", "Quote", "Clip", ["Hook 1"]);

        var useCase = CreateUseCase(
            tenant,
            request,
            brief,
            primaryAsset,
            caption,
            bundle,
            out _,
            out _,
            out var approvalRepository,
            out var schedulingRepository);

        var result = await useCase.ExecuteAsync(
            new ReviewAndScheduleDailyContentCommand(
                new ReviewAndScheduleDailyContentRequest(tenant.TenantId.Value, request.DailyContentRequestId)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("High", result.Value!.RiskLevel);
        Assert.Equal("NeedsChanges", result.Value.ApprovalStatus);
        Assert.Equal("Blocked", result.Value.SchedulingStatus);
        Assert.Equal(0, result.Value.ScheduledTargetCount);
        Assert.Equal(ApprovalStatus.NeedsChanges, approvalRepository.Saved!.Status);
        Assert.Equal(SchedulingStatus.Blocked, schedulingRepository.Saved!.Status);
    }

    private static ReviewAndScheduleDailyContentUseCase CreateUseCase(
        Tenant tenant,
        DailyContentRequest request,
        DailyContentBrief brief,
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        RepurposedAssetBundle bundle,
        out FakeComplianceReviewRepository complianceRepository,
        out FakeQualityReviewRepository qualityRepository,
        out FakeApprovalRequestRepository approvalRepository,
        out FakeSchedulingJobRepository schedulingRepository)
    {
        complianceRepository = new FakeComplianceReviewRepository();
        qualityRepository = new FakeQualityReviewRepository();
        approvalRepository = new FakeApprovalRequestRepository();
        schedulingRepository = new FakeSchedulingJobRepository();

        return new ReviewAndScheduleDailyContentUseCase(
            new FakeTenantRepository(tenant),
            new FakeDailyContentRequestRepository(request),
            new FakeDailyContentBriefRepository(brief),
            new FakePrimaryAssetRepository(primaryAsset),
            new FakeCaptionAssetRepository(captionAsset),
            new FakeRepurposedAssetBundleRepository(bundle),
            complianceRepository,
            qualityRepository,
            approvalRepository,
            schedulingRepository,
            new DeterministicIdGenerator(),
            new FixedClock());
    }

    private static Tenant CreateTenant(IReadOnlyList<string> platforms, IReadOnlyList<string> avoidTopics) =>
        Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "B2B consultants",
                "Content-led growth",
                "Founders",
                "Bold",
                "BOOK",
                platforms,
                ["Low visibility"],
                ["No time"],
                avoidTopics),
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

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

    private sealed class FakeDailyContentRequestRepository(DailyContentRequest request) : IDailyContentRequestRepository
    {
        public Task SaveAsync(DailyContentRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<DailyContentRequest?> FindByIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(request.DailyContentRequestId == requestId ? request : null);
    }

    private sealed class FakeDailyContentBriefRepository(DailyContentBrief brief) : IDailyContentBriefRepository
    {
        public Task SaveAsync(DailyContentBrief brief, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<DailyContentBrief?> FindByIdAsync(string briefId, CancellationToken cancellationToken) =>
            Task.FromResult(brief.DailyContentBriefId == briefId ? brief : null);
        public Task<DailyContentBrief?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(brief.DailyContentRequestId == requestId ? brief : null);
    }

    private sealed class FakePrimaryAssetRepository(PrimaryAsset asset) : IPrimaryAssetRepository
    {
        public Task SaveAsync(PrimaryAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<PrimaryAsset?> FindByIdAsync(string primaryAssetId, CancellationToken cancellationToken) =>
            Task.FromResult(asset.PrimaryAssetId == primaryAssetId ? asset : null);
        public Task<PrimaryAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(asset.DailyContentRequestId == requestId ? asset : null);
    }

    private sealed class FakeCaptionAssetRepository(CaptionAsset asset) : ICaptionAssetRepository
    {
        public Task SaveAsync(CaptionAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<CaptionAsset?> FindByIdAsync(string captionAssetId, CancellationToken cancellationToken) =>
            Task.FromResult(asset.CaptionAssetId == captionAssetId ? asset : null);
        public Task<CaptionAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(asset.DailyContentRequestId == requestId ? asset : null);
    }

    private sealed class FakeRepurposedAssetBundleRepository(RepurposedAssetBundle bundle) : IRepurposedAssetBundleRepository
    {
        public Task SaveAsync(RepurposedAssetBundle bundle, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<RepurposedAssetBundle?> FindByIdAsync(string bundleId, CancellationToken cancellationToken) =>
            Task.FromResult(bundle.RepurposedAssetBundleId == bundleId ? bundle : null);
        public Task<RepurposedAssetBundle?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(bundle.DailyContentRequestId == requestId ? bundle : null);
    }

    private sealed class FakeComplianceReviewRepository : IComplianceReviewRepository
    {
        public ComplianceReview? Saved { get; private set; }
        public Task SaveAsync(ComplianceReview review, CancellationToken cancellationToken)
        {
            Saved = review;
            return Task.CompletedTask;
        }

        public Task<ComplianceReview?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
    }

    private sealed class FakeQualityReviewRepository : IQualityReviewRepository
    {
        public QualityReview? Saved { get; private set; }
        public Task SaveAsync(QualityReview review, CancellationToken cancellationToken)
        {
            Saved = review;
            return Task.CompletedTask;
        }

        public Task<QualityReview?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
    }

    private sealed class FakeApprovalRequestRepository : IApprovalRequestRepository
    {
        public ApprovalRequest? Saved { get; private set; }
        public Task SaveAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken)
        {
            Saved = approvalRequest;
            return Task.CompletedTask;
        }

        public Task<ApprovalRequest?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
    }

    private sealed class FakeSchedulingJobRepository : ISchedulingJobRepository
    {
        public SchedulingJob? Saved { get; private set; }
        public Task SaveAsync(SchedulingJob job, CancellationToken cancellationToken)
        {
            Saved = job;
            return Task.CompletedTask;
        }

        public Task<SchedulingJob?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
    }
}
