namespace AIMultiAgentPlatform.Domain.Publishing;

public sealed record PublicationTarget(
    string Platform,
    DateTime ScheduledUtc,
    string PayloadSummary);
