using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Video;
using AIMultiAgentPlatform.Contracts.Video;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class ProcessHeyGenWebhookUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_FinalizesKnownSuccessEvent()
    {
        var job = CreateJob("video_job_001", "provider_video_001", VideoGenerationJobStatus.Submitted);
        var repository = new FakeVideoGenerationJobRepository(job);
        var finalizer = new FakeVideoGenerationJobFinalizer
        {
            Result = Result<FinalizeVideoGenerationResponse>.Success(
                new FinalizeVideoGenerationResponse(job.VideoGenerationJobId, "Completed", "video_asset_001", "https://blob.test/final.mp4"))
        };

        var useCase = new ProcessHeyGenWebhookUseCase(repository, finalizer, new FixedClock());
        var eventData = JsonSerializer.SerializeToElement(new { video_id = "provider_video_001", url = "https://heygen.test/final.mp4" });

        var result = await useCase.ExecuteAsync("video_agent.success", eventData, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Completed", result.Value!.Outcome);
        Assert.Equal("video_job_001", result.Value.VideoGenerationJobId);
        Assert.Equal("video_job_001", finalizer.LastRequest!.VideoGenerationJobId);
    }

    [Fact]
    public async Task ExecuteAsync_MarksKnownFailureEventAsFailed()
    {
        var job = CreateJob("video_job_002", "provider_video_002", VideoGenerationJobStatus.Processing);
        var repository = new FakeVideoGenerationJobRepository(job);

        var useCase = new ProcessHeyGenWebhookUseCase(repository, new FakeVideoGenerationJobFinalizer(), new FixedClock());
        var eventData = JsonSerializer.SerializeToElement(new { video_id = "provider_video_002", message = "Rendering failed." });

        var result = await useCase.ExecuteAsync("avatar_video.failed", eventData, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Failed", result.Value!.Outcome);
        Assert.Equal(VideoGenerationJobStatus.Failed, repository.Saved!.Status);
        Assert.Equal("Rendering failed.", repository.Saved.FailureReason);
        Assert.NotNull(repository.Saved.CompletedUtc);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoresUnknownProviderVideoIds()
    {
        var useCase = new ProcessHeyGenWebhookUseCase(
            new FakeVideoGenerationJobRepository(seed: null),
            new FakeVideoGenerationJobFinalizer(),
            new FixedClock());
        var eventData = JsonSerializer.SerializeToElement(new { video_id = "missing_video_001" });

        var result = await useCase.ExecuteAsync("video_agent.success", eventData, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ignored", result.Value!.Outcome);
        Assert.Equal(string.Empty, result.Value.VideoGenerationJobId);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoresUnhandledEventsWithoutRequiringVideoId()
    {
        var useCase = new ProcessHeyGenWebhookUseCase(
            new FakeVideoGenerationJobRepository(seed: null),
            new FakeVideoGenerationJobFinalizer(),
            new FixedClock());
        var eventData = JsonSerializer.SerializeToElement(new { test = "value" });

        var result = await useCase.ExecuteAsync("space.updated", eventData, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ignored", result.Value!.Outcome);
    }

    private static VideoGenerationJob CreateJob(string jobId, string providerJobId, VideoGenerationJobStatus status) =>
        new(
            jobId,
            $"daily_request_{jobId}",
            new TenantId("tenant_001"),
            $"primary_asset_{jobId}",
            "HeyGen",
            "default",
            providerJobId,
            $"Title {jobId}",
            "Script",
            "English",
            "9:16",
            status,
            string.Empty,
            new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc));

    private sealed class FakeVideoGenerationJobRepository(VideoGenerationJob? seed) : IVideoGenerationJobRepository
    {
        public VideoGenerationJob? Saved { get; private set; } = seed;

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

        public Task<IReadOnlyList<VideoGenerationJob>> ListActiveAsync(int maxCount, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VideoGenerationJob>>(Saved is null ? [] : [Saved]);
    }

    private sealed class FakeVideoGenerationJobFinalizer : IVideoGenerationJobFinalizer
    {
        public Result<FinalizeVideoGenerationResponse> Result { get; init; } =
            Result<FinalizeVideoGenerationResponse>.Success(
                new FinalizeVideoGenerationResponse("video_job_default", "Processing", string.Empty, string.Empty));

        public FinalizeVideoGenerationRequest? LastRequest { get; private set; }

        public Task<Result<FinalizeVideoGenerationResponse>> FinalizeAsync(FinalizeVideoGenerationRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 03, 26, 12, 30, 0, DateTimeKind.Utc);
    }
}
