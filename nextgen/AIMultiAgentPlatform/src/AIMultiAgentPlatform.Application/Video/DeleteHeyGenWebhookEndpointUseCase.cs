using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Video;

namespace AIMultiAgentPlatform.Application.Video;

public sealed class DeleteHeyGenWebhookEndpointUseCase
{
    private readonly IWebhookEndpointManager _webhookEndpointManager;
    private readonly IVideoWebhookEndpointRegistrationRepository _registrationRepository;

    public DeleteHeyGenWebhookEndpointUseCase(
        IWebhookEndpointManager webhookEndpointManager,
        IVideoWebhookEndpointRegistrationRepository registrationRepository)
    {
        _webhookEndpointManager = webhookEndpointManager;
        _registrationRepository = registrationRepository;
    }

    public async Task<Result<DeleteHeyGenWebhookEndpointResponse>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var registration = await _registrationRepository.FindByProviderAsync("HeyGen", cancellationToken);
        if (registration is null)
        {
            return Result<DeleteHeyGenWebhookEndpointResponse>.Failure(
                "heygen.webhook.not-found",
                "No local HeyGen webhook endpoint registration was found.");
        }

        var deletionResult = await _webhookEndpointManager.DeleteAsync(registration.EndpointId, cancellationToken);
        if (!deletionResult.Succeeded)
        {
            return Result<DeleteHeyGenWebhookEndpointResponse>.Failure(
                "heygen.webhook.delete.failed",
                deletionResult.FailureReason);
        }

        await _registrationRepository.DeleteAsync("HeyGen", cancellationToken);

        return Result<DeleteHeyGenWebhookEndpointResponse>.Success(
            new DeleteHeyGenWebhookEndpointResponse("Deleted", "HeyGen"));
    }
}
