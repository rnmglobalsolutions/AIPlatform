using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Application.Publishing;
using AIMultiAgentPlatform.Contracts.Publishing;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class PublishScheduledContentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesScheduledContentAndStoresPublishedRecords()
    {
        var tenant = CreateTenant();
        var schedulingJob = new SchedulingJob(
            "schedule_001",
            "daily_request_001",
            tenant.TenantId,
            SchedulingStatus.Scheduled,
            "Scheduled",
            new DateTime(2026, 03, 27, 12, 0, 0, DateTimeKind.Utc),
            [new PublicationTarget("Instagram", new DateTime(2026, 03, 27, 14, 0, 0, DateTimeKind.Utc), "Payload")]);
        var primaryAsset = new PrimaryAsset(
            "primary_asset_001",
            "daily_request_001",
            tenant.TenantId,
            PrimaryFormat.ShortVideo,
            "Short video: Topic",
            "Hook",
            "Body",
            "Payoff",
            "CTA",
            "Notes");
        var captionAsset = new CaptionAsset("caption_001", "daily_request_001", "Caption copy", "Prompt", "BOOK", ["#Test"]);
        var videoAsset = new GeneratedVideoAsset("video_asset_001", "video_job_001", "daily_request_001", tenant.TenantId, "primary_asset_001", "HeyGen", "provider_job_001", "Title", "https://provider.test/video.mp4", "https://blob.test/video.mp4", "Transcript", "English", "9:16", DateTime.UtcNow);
        var connectedProfile = new ConnectedPublishingProfile("publish_profile_001", tenant.TenantId, "Buffer", "Instagram", "buffer_profile_123", "token_123", "RNM Instagram", DateTime.UtcNow, DateTime.UtcNow);
        var publishedRecordRepository = new FakePublishedContentRecordRepository();

        var useCase = new PublishScheduledContentUseCase(
            new FakeTenantRepository(tenant),
            new FakeSchedulingJobRepository(schedulingJob),
            new FakePrimaryAssetRepository(primaryAsset),
            new FakeCaptionAssetRepository(captionAsset),
            new FakeGeneratedVideoAssetRepository(videoAsset),
            new FakeConnectedPublishingProfileRepository(connectedProfile),
            publishedRecordRepository,
            new FixedPublishingProviderSelector(new FakePublishingProvider(PublishingResult.Success("Instagram", "post_123", ""))),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new PublishScheduledContentRequest(tenant.TenantId.Value, schedulingJob.SchedulingJobId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Published", result.Value!.Status);
        Assert.Equal(1, result.Value.PublishedCount);
        Assert.Single(publishedRecordRepository.Saved);
        Assert.Equal(PublishedContentStatus.Published, publishedRecordRepository.Saved[0].Status);
        Assert.Equal("https://blob.test/video.mp4", publishedRecordRepository.Saved[0].AssetUrl);
    }

    private static Tenant CreateTenant() =>
        Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "Agencies",
                "AI content systems",
                "Founders",
                "Bold",
                "BOOK",
                ["Instagram"],
                ["Low engagement"],
                ["No time"],
                []),
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(tenant.TenantId.Value == tenantId ? tenant : null);
    }

    private sealed class FakeSchedulingJobRepository(SchedulingJob job) : ISchedulingJobRepository
    {
        public SchedulingJob Saved { get; private set; } = job;
        public Task SaveAsync(SchedulingJob job, CancellationToken cancellationToken)
        {
            Saved = job;
            return Task.CompletedTask;
        }
        public Task<SchedulingJob?> FindByIdAsync(string schedulingJobId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.SchedulingJobId == schedulingJobId ? Saved : null);
        public Task<SchedulingJob?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.DailyContentRequestId == requestId ? Saved : null);
    }

    private sealed class FakePrimaryAssetRepository(PrimaryAsset asset) : IPrimaryAssetRepository
    {
        public Task SaveAsync(PrimaryAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<PrimaryAsset?> FindByIdAsync(string primaryAssetId, CancellationToken cancellationToken) => Task.FromResult(asset.PrimaryAssetId == primaryAssetId ? asset : null);
        public Task<PrimaryAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) => Task.FromResult(asset.DailyContentRequestId == requestId ? asset : null);
    }

    private sealed class FakeCaptionAssetRepository(CaptionAsset asset) : ICaptionAssetRepository
    {
        public Task SaveAsync(CaptionAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<CaptionAsset?> FindByIdAsync(string captionAssetId, CancellationToken cancellationToken) => Task.FromResult(asset.CaptionAssetId == captionAssetId ? asset : null);
        public Task<CaptionAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) => Task.FromResult(asset.DailyContentRequestId == requestId ? asset : null);
    }

    private sealed class FakeGeneratedVideoAssetRepository(GeneratedVideoAsset asset) : IGeneratedVideoAssetRepository
    {
        public Task SaveAsync(GeneratedVideoAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<GeneratedVideoAsset?> FindByJobIdAsync(string videoGenerationJobId, CancellationToken cancellationToken) => Task.FromResult(asset.VideoGenerationJobId == videoGenerationJobId ? asset : null);
        public Task<GeneratedVideoAsset?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) => Task.FromResult(asset.DailyContentRequestId == dailyContentRequestId ? asset : null);
    }

    private sealed class FakeConnectedPublishingProfileRepository(ConnectedPublishingProfile profile) : IConnectedPublishingProfileRepository
    {
        public Task SaveAsync(ConnectedPublishingProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ConnectedPublishingProfile?> FindByTenantAndPlatformAsync(string tenantId, string platform, CancellationToken cancellationToken) =>
            Task.FromResult(profile.TenantId.Value == tenantId && profile.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) ? profile : null);
        public Task<IReadOnlyList<ConnectedPublishingProfile>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConnectedPublishingProfile>>(profile.TenantId.Value == tenantId ? [profile] : Array.Empty<ConnectedPublishingProfile>());
    }

    private sealed class FakePublishedContentRecordRepository : IPublishedContentRecordRepository
    {
        public List<PublishedContentRecord> Saved { get; } = [];
        public Task SaveAsync(PublishedContentRecord record, CancellationToken cancellationToken)
        {
            Saved.Add(record);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<PublishedContentRecord>> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublishedContentRecord>>(Saved.Where(item => item.DailyContentRequestId == dailyContentRequestId).ToArray());
    }

    private sealed class FakePublishingProvider(PublishingResult result) : IPublishingProvider
    {
        public string ProviderName => "Buffer";
        public Task<PublishingResult> PublishAsync(PublishingRequest request, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FixedPublishingProviderSelector(IPublishingProvider provider) : IPublishingProviderSelector
    {
        public IPublishingProvider? Resolve(string providerName) => provider.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase) ? provider : null;
    }

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
        public DateTime UtcNow => new(2026, 03, 27, 15, 0, 0, DateTimeKind.Utc);
    }
}
