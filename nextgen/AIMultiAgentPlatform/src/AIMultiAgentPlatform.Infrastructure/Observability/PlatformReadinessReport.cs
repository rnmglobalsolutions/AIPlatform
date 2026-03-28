namespace AIMultiAgentPlatform.Infrastructure.Observability;

public sealed record PlatformReadinessReport(
    string PlatformMode,
    string PersistenceMode,
    string MessagingMode,
    string HostingMode,
    string OverallStatus,
    DateTimeOffset CheckedUtc,
    IReadOnlyList<PlatformComponentHealth> Components)
{
    public bool IsReady => string.Equals(OverallStatus, "Healthy", StringComparison.Ordinal);
}
