using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryPublishingSecretStore : IPublishingSecretStore
{
    private readonly ConcurrentDictionary<string, PublishingAccessTokenSecret> _items = new(StringComparer.Ordinal);

    public Task SaveAccessTokenAsync(PublishingAccessTokenSecret secret, CancellationToken cancellationToken)
    {
        _items[secret.SecretReference] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAccessTokenAsync(string secretReference, CancellationToken cancellationToken) =>
        Task.FromResult(
            _items.TryGetValue(secretReference, out var secret)
                ? secret.AccessToken
                : null);
}
