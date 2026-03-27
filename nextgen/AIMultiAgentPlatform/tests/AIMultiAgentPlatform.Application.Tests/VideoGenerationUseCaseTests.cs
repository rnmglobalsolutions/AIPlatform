using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Video;
using AIMultiAgentPlatform.Contracts.Video;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class VideoGenerationUseCaseTests
{
    [Fact]
    public async Task RequestVideoGenerationUseCase_SubmitsShortVideoAndPersistsJob()
    {
        var tenant = CreateTenant();
        var dailyRequest = new DailyContentRequest("daily_request_001", tenant.TenantId, "backlog_001", 1, DateTime.UtcNow, "corr-123");
        var primaryAsset = new PrimaryAsset(
            "primary_asset_001",
            dailyRequest.DailyContentRequestId,
            tenant.TenantId,
            PrimaryFormat.ShortVideo,
            "Short video: Authority topic",
            "Open with the hidden edge.",
            "BODY: Teach the smarter move.",
            "Give one next step.",
            "Comment BOOK to get the next step.",
            "HeyGen-compatible");
        var jobRepository = new FakeVideoGenerationJobRepository();

        var useCase = new RequestVideoGenerationUseCase(
            new FakeTenantRepository(tenant),
            new FakeDailyContentRequestRepository(dailyRequest),
            new FakePrimaryAssetRepository(primaryAsset),
            jobRepository,
            new FakeVideoProvider(
                VideoGenerationSubmissionResult.Accepted("provider_job_001", "FakeVideoProvider"),
                new VideoGenerationStatusResult("provider_job_001", "FakeVideoProvider", "Completed", "https://video.test/final.mp4", "Transcript", string.Empty)),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new RequestVideoGenerationRequest(tenant.TenantId.Value, dailyRequest.DailyContentRequestId, CorrelationId: "corr-123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("video_job_001", result.Value!.VideoGenerationJobId);
        Assert.NotNull(jobRepository.Saved);
        Assert.Equal(VideoGenerationJobStatus.Submitted, jobRepository.Saved!.Status);
        Assert.Contains("Open with the hidden edge.", jobRepository.Saved.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestVideoGenerationUseCase_RejectsGraphicPrimaryAsset()
    {
        var tenant = CreateTenant();
        var dailyRequest = new DailyContentRequest("daily_request_002", tenant.TenantId, "backlog_001", 1, DateTime.UtcNow, "corr-123");
        var primaryAsset = new PrimaryAsset(
            "primary_asset_002",
            dailyRequest.DailyContentRequestId,
            tenant.TenantId,
            PrimaryFormat.BrandedGraphic,
            "Graphic post: Topic",
            "Hook",
            "Body",
            "Payoff",
            "CTA",
            "Canva");

        var useCase = new RequestVideoGenerationUseCase(
            new FakeTenantRepository(tenant),
            new FakeDailyContentRequestRepository(dailyRequest),
            new FakePrimaryAssetRepository(primaryAsset),
            new FakeVideoGenerationJobRepository(),
            new FakeVideoProvider(
                VideoGenerationSubmissionResult.Accepted("provider_job_001", "FakeVideoProvider"),
                new VideoGenerationStatusResult("provider_job_001", "FakeVideoProvider", "Completed", "https://video.test/final.mp4", "Transcript", string.Empty)),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new RequestVideoGenerationRequest(tenant.TenantId.Value, dailyRequest.DailyContentRequestId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("video.primary-asset.invalid-format", result.ErrorCode);
    }

    [Fact]
    public async Task RequestVideoGenerationUseCase_PersistsRejectedJobWhenProviderRejects()
    {
        var tenant = CreateTenant();
        var dailyRequest = new DailyContentRequest("daily_request_004", tenant.TenantId, "backlog_001", 1, DateTime.UtcNow, "corr-123");
        var primaryAsset = new PrimaryAsset(
            "primary_asset_004",
            dailyRequest.DailyContentRequestId,
            tenant.TenantId,
            PrimaryFormat.ShortVideo,
            "Short video: Topic",
            "Hook",
            "Body",
            "Payoff",
            "CTA",
            "HeyGen-compatible");
        var jobRepository = new FakeVideoGenerationJobRepository();

        var useCase = new RequestVideoGenerationUseCase(
            new FakeTenantRepository(tenant),
            new FakeDailyContentRequestRepository(dailyRequest),
            new FakePrimaryAssetRepository(primaryAsset),
            jobRepository,
            new FakeVideoProvider(
                VideoGenerationSubmissionResult.Rejected("FakeVideoProvider", "Provider quota exceeded."),
                new VideoGenerationStatusResult(string.Empty, "FakeVideoProvider", "Rejected", string.Empty, string.Empty, "Provider quota exceeded.")),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new RequestVideoGenerationRequest(tenant.TenantId.Value, dailyRequest.DailyContentRequestId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("video.submission.rejected", result.ErrorCode);
        Assert.NotNull(jobRepository.Saved);
        Assert.Equal(VideoGenerationJobStatus.Rejected, jobRepository.Saved!.Status);
        Assert.Equal("Provider quota exceeded.", jobRepository.Saved.FailureReason);
    }

    [Fact]
    public async Task RequestVideoGenerationUseCase_RejectsEmptyScript()
    {
        var tenant = CreateTenant();
        var dailyRequest = new DailyContentRequest("daily_request_005", tenant.TenantId, "backlog_001", 1, DateTime.UtcNow, "corr-123");
        var primaryAsset = new PrimaryAsset(
            "primary_asset_005",
            dailyRequest.DailyContentRequestId,
            tenant.TenantId,
            PrimaryFormat.ShortVideo,
            "Short video: Topic",
            "",
            "",
            "",
            "",
            "HeyGen-compatible");

        var useCase = new RequestVideoGenerationUseCase(
            new FakeTenantRepository(tenant),
            new FakeDailyContentRequestRepository(dailyRequest),
            new FakePrimaryAssetRepository(primaryAsset),
            new FakeVideoGenerationJobRepository(),
            new FakeVideoProvider(
                VideoGenerationSubmissionResult.Accepted("provider_job_005", "FakeVideoProvider"),
                new VideoGenerationStatusResult("provider_job_005", "FakeVideoProvider", "Completed", "https://video.test/final.mp4", "Transcript", string.Empty)),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new RequestVideoGenerationRequest(tenant.TenantId.Value, dailyRequest.DailyContentRequestId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("video.primary-asset.empty-script", result.ErrorCode);
    }

    [Fact]
    public async Task FinalizeVideoGenerationUseCase_PersistsGeneratedAssetWhenProviderCompletes()
    {
        var tenant = CreateTenant();
        var job = new VideoGenerationJob(
            "video_job_001",
            "daily_request_003",
            tenant.TenantId,
            "primary_asset_003",
            "FakeVideoProvider",
            "default",
            "provider_job_003",
            "Short video: Topic",
            "Script",
            "English",
            "9:16",
            VideoGenerationJobStatus.Submitted,
            string.Empty,
            new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc));
        var jobRepository = new FakeVideoGenerationJobRepository { Saved = job };
        var assetRepository = new FakeGeneratedVideoAssetRepository();

        var useCase = new FinalizeVideoGenerationUseCase(
            new FakeTenantRepository(tenant),
            jobRepository,
            assetRepository,
            new FakeVideoProvider(
                VideoGenerationSubmissionResult.Accepted("provider_job_003", "FakeVideoProvider"),
                new VideoGenerationStatusResult("provider_job_003", "FakeVideoProvider", "Completed", "https://video.test/final.mp4", "Transcript text", string.Empty)),
            new FakeVideoAssetStore("https://blob.test/final.mp4"),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new FinalizeVideoGenerationRequest(tenant.TenantId.Value, job.VideoGenerationJobId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Completed", result.Value!.Status);
        Assert.Equal("video_asset_001", result.Value.GeneratedVideoAssetId);
        Assert.NotNull(assetRepository.Saved);
        Assert.Equal("https://blob.test/final.mp4", assetRepository.Saved!.VideoUrl);
        Assert.Equal("https://video.test/final.mp4", assetRepository.Saved.ProviderVideoUrl);
        Assert.Equal(VideoGenerationJobStatus.Completed, jobRepository.Saved!.Status);
    }

    [Fact]
    public async Task FinalizeVideoGenerationUseCase_ReturnsExistingAssetWithoutPollingProviderWhenJobAlreadyCompleted()
    {
        var tenant = CreateTenant();
        var job = new VideoGenerationJob(
            "video_job_004",
            "daily_request_004",
            tenant.TenantId,
            "primary_asset_004",
            "FakeVideoProvider",
            "default",
            "provider_job_004",
            "Short video: Topic",
            "Script",
            "English",
            "9:16",
            VideoGenerationJobStatus.Completed,
            string.Empty,
            new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 03, 26, 12, 5, 0, DateTimeKind.Utc),
            new DateTime(2026, 03, 26, 12, 10, 0, DateTimeKind.Utc));
        var jobRepository = new FakeVideoGenerationJobRepository { Saved = job };
        var existingAsset = new GeneratedVideoAsset(
            "video_asset_existing",
            job.VideoGenerationJobId,
            job.DailyContentRequestId,
            tenant.TenantId,
            job.PrimaryAssetId,
            job.ProviderName,
            job.ProviderJobId,
            job.Title,
            "https://video.test/existing-source.mp4",
            "https://video.test/existing.mp4",
            "Transcript",
            job.Language,
            job.AspectRatio,
            new DateTime(2026, 03, 26, 12, 10, 0, DateTimeKind.Utc));
        var assetRepository = new FakeGeneratedVideoAssetRepository { Saved = existingAsset };
        var provider = new FakeVideoProvider(
            VideoGenerationSubmissionResult.Accepted("provider_job_004", "FakeVideoProvider"),
            new VideoGenerationStatusResult("provider_job_004", "FakeVideoProvider", "Completed", "https://video.test/final.mp4", "Transcript text", string.Empty));

        var useCase = new FinalizeVideoGenerationUseCase(
            new FakeTenantRepository(tenant),
            jobRepository,
            assetRepository,
            provider,
            new FakeVideoAssetStore("https://blob.test/existing.mp4"),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new FinalizeVideoGenerationRequest(tenant.TenantId.Value, job.VideoGenerationJobId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("video_asset_existing", result.Value!.GeneratedVideoAssetId);
        Assert.Equal("https://video.test/existing.mp4", result.Value.VideoUrl);
        Assert.Equal(0, provider.GetStatusCalls);
    }

    [Fact]
    public async Task FinalizeVideoGenerationUseCase_ReturnsFailureWhenProviderFails()
    {
        var tenant = CreateTenant();
        var job = new VideoGenerationJob(
            "video_job_005",
            "daily_request_005",
            tenant.TenantId,
            "primary_asset_005",
            "FakeVideoProvider",
            "default",
            "provider_job_005",
            "Short video: Topic",
            "Script",
            "English",
            "9:16",
            VideoGenerationJobStatus.Submitted,
            string.Empty,
            new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc));
        var jobRepository = new FakeVideoGenerationJobRepository { Saved = job };
        var assetRepository = new FakeGeneratedVideoAssetRepository();

        var useCase = new FinalizeVideoGenerationUseCase(
            new FakeTenantRepository(tenant),
            jobRepository,
            assetRepository,
            new FakeVideoProvider(
                VideoGenerationSubmissionResult.Accepted("provider_job_005", "FakeVideoProvider"),
                new VideoGenerationStatusResult("provider_job_005", "FakeVideoProvider", "Failed", string.Empty, string.Empty, "Render failed.")),
            new FakeVideoAssetStore("https://blob.test/final.mp4"),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new FinalizeVideoGenerationRequest(tenant.TenantId.Value, job.VideoGenerationJobId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("video.job.failed", result.ErrorCode);
        Assert.Equal(VideoGenerationJobStatus.Failed, jobRepository.Saved!.Status);
        Assert.Equal("Render failed.", jobRepository.Saved.FailureReason);
        Assert.Null(assetRepository.Saved);
    }

    [Fact]
    public async Task FinalizeVideoGenerationUseCase_ReturnsFailureWhenCompletedWithoutDownloadUrl()
    {
        var tenant = CreateTenant();
        var job = new VideoGenerationJob(
            "video_job_006",
            "daily_request_006",
            tenant.TenantId,
            "primary_asset_006",
            "FakeVideoProvider",
            "default",
            "provider_job_006",
            "Short video: Topic",
            "Script",
            "English",
            "9:16",
            VideoGenerationJobStatus.Submitted,
            string.Empty,
            new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc));
        var jobRepository = new FakeVideoGenerationJobRepository { Saved = job };

        var useCase = new FinalizeVideoGenerationUseCase(
            new FakeTenantRepository(tenant),
            jobRepository,
            new FakeGeneratedVideoAssetRepository(),
            new FakeVideoProvider(
                VideoGenerationSubmissionResult.Accepted("provider_job_006", "FakeVideoProvider"),
                new VideoGenerationStatusResult("provider_job_006", "FakeVideoProvider", "Completed", string.Empty, "Transcript text", string.Empty)),
            new FakeVideoAssetStore("https://blob.test/final.mp4"),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new FinalizeVideoGenerationRequest(tenant.TenantId.Value, job.VideoGenerationJobId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("video.job.missing-download-url", result.ErrorCode);
    }

    [Fact]
    public async Task FinalizeVideoGenerationUseCase_ReturnsFailureWhenAssetStoreFails()
    {
        var tenant = CreateTenant();
        var job = new VideoGenerationJob(
            "video_job_007",
            "daily_request_007",
            tenant.TenantId,
            "primary_asset_007",
            "FakeVideoProvider",
            "default",
            "provider_job_007",
            "Short video: Topic",
            "Script",
            "English",
            "9:16",
            VideoGenerationJobStatus.Submitted,
            string.Empty,
            new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc));
        var jobRepository = new FakeVideoGenerationJobRepository { Saved = job };

        var useCase = new FinalizeVideoGenerationUseCase(
            new FakeTenantRepository(tenant),
            jobRepository,
            new FakeGeneratedVideoAssetRepository(),
            new FakeVideoProvider(
                VideoGenerationSubmissionResult.Accepted("provider_job_007", "FakeVideoProvider"),
                new VideoGenerationStatusResult("provider_job_007", "FakeVideoProvider", "Completed", "https://video.test/final.mp4", "Transcript text", string.Empty)),
            new FakeVideoAssetStore(null, "Blob write failed."),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new FinalizeVideoGenerationRequest(tenant.TenantId.Value, job.VideoGenerationJobId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("video.asset-store.failed", result.ErrorCode);
    }

    private static Tenant CreateTenant() =>
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
                ["Instagram"],
                ["Low visibility"],
                ["No time"],
                []),
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
        public DateTime UtcNow => new(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc);
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

    private sealed class FakePrimaryAssetRepository(PrimaryAsset asset) : IPrimaryAssetRepository
    {
        public Task SaveAsync(PrimaryAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<PrimaryAsset?> FindByIdAsync(string primaryAssetId, CancellationToken cancellationToken) =>
            Task.FromResult(asset.PrimaryAssetId == primaryAssetId ? asset : null);

        public Task<PrimaryAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(asset.DailyContentRequestId == requestId ? asset : null);
    }

    private sealed class FakeVideoGenerationJobRepository : IVideoGenerationJobRepository
    {
        public VideoGenerationJob? Saved { get; set; }

        public Task SaveAsync(VideoGenerationJob job, CancellationToken cancellationToken)
        {
            Saved = job;
            return Task.CompletedTask;
        }

        public Task<VideoGenerationJob?> FindByIdAsync(string videoGenerationJobId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.VideoGenerationJobId == videoGenerationJobId ? Saved : null);

        public Task<VideoGenerationJob?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == dailyContentRequestId ? Saved : null);

        public Task<VideoGenerationJob?> FindByProviderJobIdAsync(string providerJobId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.ProviderJobId == providerJobId ? Saved : null);

        public Task<IReadOnlyList<VideoGenerationJob>> ListActiveAsync(int maxCount, CancellationToken cancellationToken)
        {
            IReadOnlyList<VideoGenerationJob> jobs = Saved is not null &&
                                                     Saved.Status is VideoGenerationJobStatus.Submitted or VideoGenerationJobStatus.Processing
                ? [Saved]
                : Array.Empty<VideoGenerationJob>();

            return Task.FromResult(jobs);
        }
    }

    private sealed class FakeGeneratedVideoAssetRepository : IGeneratedVideoAssetRepository
    {
        public GeneratedVideoAsset? Saved { get; set; }

        public Task SaveAsync(GeneratedVideoAsset asset, CancellationToken cancellationToken)
        {
            Saved = asset;
            return Task.CompletedTask;
        }

        public Task<GeneratedVideoAsset?> FindByJobIdAsync(string videoGenerationJobId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.VideoGenerationJobId == videoGenerationJobId ? Saved : null);

        public Task<GeneratedVideoAsset?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == dailyContentRequestId ? Saved : null);
    }

    private sealed class FakeVideoProvider(
        VideoGenerationSubmissionResult submissionResult,
        VideoGenerationStatusResult statusResult) : IVideoGenerationProvider
    {
        public string ProviderName => "FakeVideoProvider";

        public int GetStatusCalls { get; private set; }

        public Task<VideoGenerationSubmissionResult> SubmitAsync(VideoGenerationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(submissionResult);

        public Task<VideoGenerationStatusResult> GetStatusAsync(string providerJobId, CancellationToken cancellationToken)
        {
            GetStatusCalls++;
            return Task.FromResult(statusResult);
        }
    }

    private sealed class FakeVideoAssetStore(string? videoUrl, string? failureReason = null) : IVideoAssetStore
    {
        public Task<VideoAssetStorageResult> StoreAsync(VideoAssetStorageRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(
                string.IsNullOrWhiteSpace(failureReason)
                    ? VideoAssetStorageResult.Success(videoUrl ?? request.ProviderVideoUrl, "FakeVideoAssetStore")
                    : VideoAssetStorageResult.Failure("FakeVideoAssetStore", failureReason));
    }
}
