namespace AIMultiAgentPlatform.Application.Abstractions.Publishing;

public interface IPublishingSecretStore
{
    Task SaveAccessTokenAsync(PublishingAccessTokenSecret secret, CancellationToken cancellationToken);

    Task<string?> GetAccessTokenAsync(string secretReference, CancellationToken cancellationToken);
}
