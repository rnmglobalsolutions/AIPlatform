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
        Task.FromResult(_items.Values
            .Where(item =>
                item.TenantId.Value == tenantId &&
                item.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedUtc)
            .ThenBy(item => item.ProviderName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault());

    public Task<ConnectedPublishingProfile?> FindByTenantPlatformAndProviderAsync(string tenantId, string platform, string providerName, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values
            .Where(item =>
                item.TenantId.Value == tenantId &&
                item.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                item.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UpdatedUtc)
            .FirstOrDefault());

    public Task<IReadOnlyList<ConnectedPublishingProfile>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConnectedPublishingProfile>>(_items.Values
            .Where(item => item.TenantId.Value == tenantId)
            .OrderByDescending(item => item.UpdatedUtc)
            .ThenBy(item => item.Platform, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    public IReadOnlyList<ConnectedPublishingProfile> ListAll() => _items.Values.ToArray();
}
