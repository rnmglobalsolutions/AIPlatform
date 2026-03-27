using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryConnectedPublishingProfileRepository : IConnectedPublishingProfileRepository
{
    private readonly ConcurrentDictionary<string, ConnectedPublishingProfile> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(ConnectedPublishingProfile profile, CancellationToken cancellationToken)
    {
        _items[profile.ConnectedPublishingProfileId] = profile;
        return Task.CompletedTask;
    }

    public Task<ConnectedPublishingProfile?> FindByTenantAndPlatformAsync(string tenantId, string platform, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item =>
            item.TenantId.Value == tenantId &&
            item.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<ConnectedPublishingProfile>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConnectedPublishingProfile>>(_items.Values.Where(item => item.TenantId.Value == tenantId).ToArray());

    public IReadOnlyList<ConnectedPublishingProfile> ListAll() => _items.Values.ToArray();
}
