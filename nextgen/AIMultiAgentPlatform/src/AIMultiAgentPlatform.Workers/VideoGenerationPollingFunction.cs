using AIMultiAgentPlatform.Application.Video;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIMultiAgentPlatform.Workers;

public sealed class VideoGenerationPollingFunction
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly FeatureFlagOptions _featureFlags;
    private readonly ILogger<VideoGenerationPollingFunction> _logger;

    public VideoGenerationPollingFunction(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        FeatureFlagOptions featureFlags,
        ILogger<VideoGenerationPollingFunction> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _featureFlags = featureFlags;
        _logger = logger;
    }

    [Function("PollPendingVideoGenerationJobs")]
    public async Task RunAsync(
        [TimerTrigger("%VideoGenerationPollingSchedule%")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        if (!_featureFlags.EnableVideoGeneration)
        {
            _logger.LogDebug("Video generation polling skipped because EnableVideoGeneration is disabled.");
            return;
        }

        var batchSize = ResolveBatchSize();

        using var scope = _scopeFactory.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<ProcessPendingVideoGenerationJobsUseCase>();
        var result = await useCase.ExecuteAsync(batchSize, cancellationToken);

        _logger.LogInformation(
            "Video generation polling completed. Discovered={JobsDiscovered}, Processed={JobsProcessed}, Completed={JobsCompleted}, Pending={JobsStillPending}, Failed={JobsFailed}, ScheduleStatusNext={NextOccurrence}",
            result.JobsDiscovered,
            result.JobsProcessed,
            result.JobsCompleted,
            result.JobsStillPending,
            result.JobsFailed,
            timerInfo.ScheduleStatus?.Next);
    }

    private int ResolveBatchSize()
    {
        var configuredValue = _configuration["VideoGenerationPollingBatchSize"];
        return int.TryParse(configuredValue, out var parsed) && parsed > 0 ? parsed : 10;
    }
}
