using AIMultiAgentPlatform.Application.Abstractions;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
