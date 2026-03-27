using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IConnectedPublishingProfileRepository
{
    Task SaveAsync(ConnectedPublishingProfile profile, CancellationToken cancellationToken);

    Task<ConnectedPublishingProfile?> FindByTenantAndPlatformAsync(string tenantId, string platform, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectedPublishingProfile>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken);
}
