using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IVideoWebhookEndpointRegistrationRepository
{
    Task SaveAsync(VideoWebhookEndpointRegistration registration, CancellationToken cancellationToken);

    Task<VideoWebhookEndpointRegistration?> FindByProviderAsync(string providerName, CancellationToken cancellationToken);

    Task DeleteAsync(string providerName, CancellationToken cancellationToken);
}
