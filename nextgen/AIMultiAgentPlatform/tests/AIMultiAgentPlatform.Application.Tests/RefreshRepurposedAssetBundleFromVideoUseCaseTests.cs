using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Content;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class RefreshRepurposedAssetBundleFromVideoUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_RebuildsRepurposeBundleFromTranscript()
    {
        var requestId = "daily_request_200";
        var generatedVideoAsset = new GeneratedVideoAsset(
            "video_asset_200",
            "video_job_200",
            requestId,
            new TenantId("tenant_200"),
            "primary_asset_200",
            "HeyGen",
            "provider_job_200",
            "Short video: Authority",
            "https://provider.test/video.mp4",
            "https://blob.test/video.mp4",
            "Most businesses are posting every day but still not generating leads. The real issue is weak positioning. Stronger positioning makes every post convert better.",
            "English",
            "9:16",
            new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc),
            [
                new TimedTranscriptSegment(0, 3.2, "Most businesses are posting every day but still not generating leads."),
                new TimedTranscriptSegment(3.2, 6.8, "The real issue is weak positioning."),
                new TimedTranscriptSegment(6.8, 10.5, "Stronger positioning makes every post convert better.")
            ]);
        var primaryAsset = new PrimaryAsset(
            "primary_asset_200",
            requestId,
            new TenantId("tenant_200"),
            PrimaryFormat.ShortVideo,
            "Short video: Authority",
            "Most businesses are posting every day.",
            "Weak positioning is the real issue.",
            "Fix the message and your content converts better.",
            "Comment BOOK for the next step.",
            "HeyGen");
        var captionAsset = new CaptionAsset(
            "caption_200",
            requestId,
            "Caption",
            "What do you think?",
            "BOOK",
            ["#marketing"]);
        var repurposedBundleRepository = new FakeRepurposedAssetBundleRepository(
            new RepurposedAssetBundle(
                "repurpose_200",
                requestId,
                "Old carousel",
                ["Old story"],
                "Old LinkedIn",
                "\"Old quote\"",
                "Old clip",
                ["Old hook"]));

        var useCase = new RefreshRepurposedAssetBundleFromVideoUseCase(
            new FakeGeneratedVideoAssetRepository(generatedVideoAsset),
            repurposedBundleRepository,
            new FakePrimaryAssetRepository(primaryAsset),
            new FakeCaptionAssetRepository(captionAsset));

        var result = await useCase.ExecuteAsync(
            new RefreshRepurposedAssetBundleFromVideoRequest(requestId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repurposedBundleRepository.Saved);
        Assert.Contains("weak positioning", repurposedBundleRepository.Saved!.CarouselOutline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Comment or DM 'BOOK'", repurposedBundleRepository.Saved.CarouselOutline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stronger positioning", repurposedBundleRepository.Saved.LinkedInPost, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Comment BOOK", repurposedBundleRepository.Saved.CommentHooks.Last(), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(repurposedBundleRepository.Saved.ClipPlans);
        Assert.Equal(3, repurposedBundleRepository.Saved.ClipPlans!.Count);
        Assert.Equal("Hook Clip", repurposedBundleRepository.Saved.ClipPlans[0].Label);
        Assert.Equal(0d, repurposedBundleRepository.Saved.ClipPlans[0].StartSeconds);
        Assert.Contains("0s to 10.5s", repurposedBundleRepository.Saved.ShortClipIdea, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeGeneratedVideoAssetRepository(GeneratedVideoAsset asset) : IGeneratedVideoAssetRepository
    {
        public Task SaveAsync(GeneratedVideoAsset asset, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<GeneratedVideoAsset?> FindByJobIdAsync(string videoGenerationJobId, CancellationToken cancellationToken) =>
            Task.FromResult(asset.VideoGenerationJobId == videoGenerationJobId ? asset : null);

        public Task<GeneratedVideoAsset?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult(asset.DailyContentRequestId == dailyContentRequestId ? asset : null);
    }

    private sealed class FakeRepurposedAssetBundleRepository(RepurposedAssetBundle bundle) : IRepurposedAssetBundleRepository
    {
        public RepurposedAssetBundle? Saved { get; private set; } = bundle;

        public Task SaveAsync(RepurposedAssetBundle bundle, CancellationToken cancellationToken)
        {
            Saved = bundle;
            return Task.CompletedTask;
        }

        public Task<RepurposedAssetBundle?> FindByIdAsync(string bundleId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.RepurposedAssetBundleId == bundleId ? Saved : null);

        public Task<RepurposedAssetBundle?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
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
}
