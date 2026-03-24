namespace AIMultiAgentPlatform.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
