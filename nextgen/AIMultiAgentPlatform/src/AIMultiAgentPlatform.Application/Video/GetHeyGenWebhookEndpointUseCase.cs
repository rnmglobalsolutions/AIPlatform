using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Video;

namespace AIMultiAgentPlatform.Application.Video;

public sealed class GetHeyGenWebhookEndpointUseCase
{
    private readonly IVideoWebhookEndpointRegistrationRepository _registrationRepository;

    public GetHeyGenWebhookEndpointUseCase(IVideoWebhookEndpointRegistrationRepository registrationRepository) =>
        _registrationRepository = registrationRepository;

    public async Task<Result<GetHeyGenWebhookEndpointResponse>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var registration = await _registrationRepository.FindByProviderAsync("HeyGen", cancellationToken);
        if (registration is null)
        {
            return Result<GetHeyGenWebhookEndpointResponse>.Failure(
                "heygen.webhook.not-found",
                "No local HeyGen webhook endpoint registration was found.");
        }

        return Result<GetHeyGenWebhookEndpointResponse>.Success(
            new GetHeyGenWebhookEndpointResponse(
                registration.ProviderName,
                registration.EndpointId,
                registration.Url,
                registration.Status,
                registration.Events,
                !string.IsNullOrWhiteSpace(registration.Secret),
                registration.LastSyncedUtc));
    }
}
