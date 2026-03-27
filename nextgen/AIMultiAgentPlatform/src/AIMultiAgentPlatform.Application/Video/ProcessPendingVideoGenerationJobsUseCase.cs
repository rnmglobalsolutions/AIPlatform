using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Contracts.Video;

namespace AIMultiAgentPlatform.Application.Video;

public sealed class ProcessPendingVideoGenerationJobsUseCase
{
    private readonly IVideoGenerationJobRepository _videoGenerationJobRepository;
    private readonly IVideoGenerationJobFinalizer _videoGenerationJobFinalizer;

    public ProcessPendingVideoGenerationJobsUseCase(
        IVideoGenerationJobRepository videoGenerationJobRepository,
        IVideoGenerationJobFinalizer videoGenerationJobFinalizer)
    {
        _videoGenerationJobRepository = videoGenerationJobRepository;
        _videoGenerationJobFinalizer = videoGenerationJobFinalizer;
    }

    public async Task<ProcessPendingVideoGenerationJobsResponse> ExecuteAsync(
        int maxJobs,
        CancellationToken cancellationToken)
    {
        var jobs = await _videoGenerationJobRepository.ListActiveAsync(maxJobs, cancellationToken);
        var completed = 0;
        var stillPending = 0;
        var failed = 0;

        foreach (var job in jobs)
        {
            var result = await _videoGenerationJobFinalizer.FinalizeAsync(
                new FinalizeVideoGenerationRequest(job.TenantId.Value, job.VideoGenerationJobId),
                cancellationToken);

            if (result.IsFailure)
            {
                failed++;
                continue;
            }

            if (string.Equals(result.Value?.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                completed++;
            }
            else
            {
                stillPending++;
            }
        }

        return new ProcessPendingVideoGenerationJobsResponse(
            jobs.Count,
            jobs.Count,
            completed,
            stillPending,
            failed);
    }
}
