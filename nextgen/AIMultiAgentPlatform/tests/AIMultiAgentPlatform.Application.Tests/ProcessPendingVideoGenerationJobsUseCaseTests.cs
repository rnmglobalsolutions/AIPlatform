using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Video;
using AIMultiAgentPlatform.Contracts.Video;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class ProcessPendingVideoGenerationJobsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ProcessesOnlyActiveJobsAndAggregatesOutcomes()
    {
        var jobs = new[]
        {
            CreateJob("video_job_001", VideoGenerationJobStatus.Submitted, new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc)),
            CreateJob("video_job_002", VideoGenerationJobStatus.Processing, new DateTime(2026, 03, 26, 12, 1, 0, DateTimeKind.Utc)),
            CreateJob("video_job_003", VideoGenerationJobStatus.Completed, new DateTime(2026, 03, 26, 12, 2, 0, DateTimeKind.Utc))
        };

        var finalizer = new FakeVideoGenerationJobFinalizer();
        finalizer.Results["video_job_001"] = Result<FinalizeVideoGenerationResponse>.Success(
            new FinalizeVideoGenerationResponse("video_job_001", "Completed", "asset_001", "https://blob.test/001.mp4"));
        finalizer.Results["video_job_002"] = Result<FinalizeVideoGenerationResponse>.Success(
            new FinalizeVideoGenerationResponse("video_job_002", "Processing", string.Empty, string.Empty));

        var useCase = new ProcessPendingVideoGenerationJobsUseCase(
            new FakeVideoGenerationJobRepository(jobs),
            finalizer);

        var result = await useCase.ExecuteAsync(10, CancellationToken.None);

        Assert.Equal(2, result.JobsDiscovered);
        Assert.Equal(2, result.JobsProcessed);
        Assert.Equal(1, result.JobsCompleted);
        Assert.Equal(1, result.JobsStillPending);
        Assert.Equal(0, result.JobsFailed);
        Assert.Equal(["video_job_001", "video_job_002"], finalizer.RequestedJobIds);
    }

    [Fact]
    public async Task ExecuteAsync_TracksFailuresAndHonorsBatchSize()
    {
        var jobs = new[]
        {
            CreateJob("video_job_010", VideoGenerationJobStatus.Submitted, new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc)),
            CreateJob("video_job_011", VideoGenerationJobStatus.Submitted, new DateTime(2026, 03, 26, 12, 1, 0, DateTimeKind.Utc)),
            CreateJob("video_job_012", VideoGenerationJobStatus.Processing, new DateTime(2026, 03, 26, 12, 2, 0, DateTimeKind.Utc))
        };

        var finalizer = new FakeVideoGenerationJobFinalizer();
        finalizer.Results["video_job_010"] = Result<FinalizeVideoGenerationResponse>.Failure("video.job.failed", "Render failed.");
        finalizer.Results["video_job_011"] = Result<FinalizeVideoGenerationResponse>.Success(
            new FinalizeVideoGenerationResponse("video_job_011", "Completed", "asset_011", "https://blob.test/011.mp4"));

        var useCase = new ProcessPendingVideoGenerationJobsUseCase(
            new FakeVideoGenerationJobRepository(jobs),
            finalizer);

        var result = await useCase.ExecuteAsync(2, CancellationToken.None);

        Assert.Equal(2, result.JobsDiscovered);
        Assert.Equal(2, result.JobsProcessed);
        Assert.Equal(1, result.JobsCompleted);
        Assert.Equal(0, result.JobsStillPending);
        Assert.Equal(1, result.JobsFailed);
        Assert.Equal(["video_job_010", "video_job_011"], finalizer.RequestedJobIds);
    }

    private static VideoGenerationJob CreateJob(string jobId, VideoGenerationJobStatus status, DateTime requestedUtc) =>
        new(
            jobId,
            $"daily_request_{jobId}",
            new TenantId("tenant_001"),
            $"primary_asset_{jobId}",
            "FakeVideoProvider",
            "default",
            $"provider_{jobId}",
            $"Title {jobId}",
            "Script",
            "English",
            "9:16",
            status,
            string.Empty,
            requestedUtc);

    private sealed class FakeVideoGenerationJobRepository(IReadOnlyList<VideoGenerationJob> jobs) : IVideoGenerationJobRepository
    {
        public Task SaveAsync(VideoGenerationJob job, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<VideoGenerationJob?> FindByIdAsync(string videoGenerationJobId, CancellationToken cancellationToken) =>
            Task.FromResult(jobs.FirstOrDefault(job => job.VideoGenerationJobId == videoGenerationJobId));

        public Task<VideoGenerationJob?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult(jobs.FirstOrDefault(job => job.DailyContentRequestId == dailyContentRequestId));

        public Task<VideoGenerationJob?> FindByProviderJobIdAsync(string providerJobId, CancellationToken cancellationToken) =>
            Task.FromResult(jobs.FirstOrDefault(job => job.ProviderJobId == providerJobId));

        public Task<IReadOnlyList<VideoGenerationJob>> ListActiveAsync(int maxCount, CancellationToken cancellationToken)
        {
            var take = maxCount <= 0 ? 10 : maxCount;
            var activeJobs = jobs
                .Where(job => job.Status is VideoGenerationJobStatus.Submitted or VideoGenerationJobStatus.Processing)
                .OrderBy(job => job.RequestedUtc)
                .Take(take)
                .ToArray();

            return Task.FromResult<IReadOnlyList<VideoGenerationJob>>(activeJobs);
        }
    }

    private sealed class FakeVideoGenerationJobFinalizer : IVideoGenerationJobFinalizer
    {
        public Dictionary<string, Result<FinalizeVideoGenerationResponse>> Results { get; } = new(StringComparer.Ordinal);

        public List<string> RequestedJobIds { get; } = [];

        public Task<Result<FinalizeVideoGenerationResponse>> FinalizeAsync(FinalizeVideoGenerationRequest request, CancellationToken cancellationToken)
        {
            RequestedJobIds.Add(request.VideoGenerationJobId);

            if (Results.TryGetValue(request.VideoGenerationJobId, out var result))
            {
                return Task.FromResult(result);
            }

            return Task.FromResult(
                Result<FinalizeVideoGenerationResponse>.Success(
                    new FinalizeVideoGenerationResponse(request.VideoGenerationJobId, "Processing", string.Empty, string.Empty)));
        }
    }
}
