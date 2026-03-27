using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IContentMemoryRepository
{
    Task SaveAsync(ContentMemoryEntry entry, CancellationToken cancellationToken);

    Task<ContentMemorySnapshot> GetSnapshotAsync(string tenantId, int maxEntries, CancellationToken cancellationToken);
}
