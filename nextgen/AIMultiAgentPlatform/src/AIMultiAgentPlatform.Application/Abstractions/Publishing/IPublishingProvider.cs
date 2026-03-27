namespace AIMultiAgentPlatform.Application.Abstractions.Publishing;

public interface IPublishingProvider
{
    string ProviderName { get; }

    Task<PublishingResult> PublishAsync(PublishingRequest request, CancellationToken cancellationToken);
}
