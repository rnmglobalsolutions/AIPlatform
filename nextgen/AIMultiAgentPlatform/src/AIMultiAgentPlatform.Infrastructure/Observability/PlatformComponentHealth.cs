namespace AIMultiAgentPlatform.Infrastructure.Observability;

public sealed record PlatformComponentHealth(
    string Name,
    string Status,
    bool Required,
    bool Ready,
    string Detail);
