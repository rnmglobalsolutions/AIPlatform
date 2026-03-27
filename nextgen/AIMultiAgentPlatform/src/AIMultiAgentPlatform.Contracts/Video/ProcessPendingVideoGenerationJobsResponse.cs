namespace AIMultiAgentPlatform.Contracts.Video;

public sealed record ProcessPendingVideoGenerationJobsResponse(
    int JobsDiscovered,
    int JobsProcessed,
    int JobsCompleted,
    int JobsStillPending,
    int JobsFailed);
