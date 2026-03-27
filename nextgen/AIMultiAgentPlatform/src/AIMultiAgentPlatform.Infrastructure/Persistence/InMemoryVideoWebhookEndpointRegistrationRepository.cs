using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryVideoWebhookEndpointRegistrationRepository : IVideoWebhookEndpointRegistrationRepository
{
    private readonly ConcurrentDictionary<string, VideoWebhookEndpointRegistration> _items = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(VideoWebhookEndpointRegistration registration, CancellationToken cancellationToken)
    {
        _items[registration.ProviderName] = registration;
        return Task.CompletedTask;
    }

    public Task<VideoWebhookEndpointRegistration?> FindByProviderAsync(string providerName, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(providerName, out var registration) ? registration : null);

    public Task DeleteAsync(string providerName, CancellationToken cancellationToken)
    {
        _items.TryRemove(providerName, out _);
        return Task.CompletedTask;
    }

    public VideoWebhookEndpointRegistration? Find(string providerName) =>
        _items.TryGetValue(providerName, out var registration) ? registration : null;
}
