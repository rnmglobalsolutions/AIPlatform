namespace AIMultiAgentPlatform.Application.Abstractions.Publishing;

public interface IPublishingProvider
{
    string ProviderName { get; }

    Task<PublishingResult> PublishAsync(PublishingRequest request, CancellationToken cancellationToken);

    Task<PublishingReconciliationResult> ReconcileAsync(PublishingReconciliationRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(PublishingReconciliationResult.Failure(request.Platform, $"{ProviderName} does not support reconciliation in the current environment."));
}
