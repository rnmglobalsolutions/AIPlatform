using AIMultiAgentPlatform.Application.Abstractions;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class GuidIdGenerator : IIdGenerator
{
    public string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
