namespace AIMultiAgentPlatform.Contracts.Publishing;

public sealed record ReconcilePublishedContentResponse(
    string SchedulingJobId,
    int RecordsProcessed,
    int SnapshotsSaved,
    int Failures);
