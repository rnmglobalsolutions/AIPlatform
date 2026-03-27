using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Video;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Video;

public sealed class EnsureHeyGenWebhookEndpointUseCase
{
    private static readonly string[] DefaultEvents =
    [
        "avatar_video.success",
        "avatar_video.failed",
        "video_agent.success",
        "video_agent.failed"
    ];

    private readonly IWebhookEndpointManager _webhookEndpointManager;
    private readonly IVideoWebhookEndpointRegistrationRepository _registrationRepository;
    private readonly IPublicWebhookUrlResolver? _publicWebhookUrlResolver;
    private readonly IClock _clock;

    public EnsureHeyGenWebhookEndpointUseCase(
        IWebhookEndpointManager webhookEndpointManager,
        IVideoWebhookEndpointRegistrationRepository registrationRepository,
        IClock clock,
        IPublicWebhookUrlResolver? publicWebhookUrlResolver = null)
    {
        _webhookEndpointManager = webhookEndpointManager;
        _registrationRepository = registrationRepository;
        _clock = clock;
        _publicWebhookUrlResolver = publicWebhookUrlResolver;
    }

    public async Task<Result<EnsureHeyGenWebhookEndpointResponse>> ExecuteAsync(
        EnsureHeyGenWebhookEndpointRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedUrl = ResolveWebhookUrl(request.PublicWebhookUrl);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return Result<EnsureHeyGenWebhookEndpointResponse>.Failure(
                "heygen.webhook.url.required",
                "PublicWebhookUrl could not be resolved for the current environment.");
        }
        var desiredEvents = NormalizeEvents(request.Events);
        var listResult = await _webhookEndpointManager.ListAsync(cancellationToken);
        if (!listResult.Succeeded)
        {
            return Result<EnsureHeyGenWebhookEndpointResponse>.Failure(
                "heygen.webhook.list.failed",
                listResult.FailureReason);
        }

        var localRegistration = await _registrationRepository.FindByProviderAsync("HeyGen", cancellationToken);
        var remoteMatch = ResolveRemoteMatch(listResult.Endpoints, localRegistration, normalizedUrl);

        WebhookEndpointMutationResult mutationResult;
        string outcome;

        if (remoteMatch is null)
        {
            mutationResult = await _webhookEndpointManager.CreateAsync(normalizedUrl, desiredEvents, cancellationToken);
            outcome = "Created";
        }
        else if (!IsEndpointUpToDate(remoteMatch, normalizedUrl, desiredEvents))
        {
            mutationResult = await _webhookEndpointManager.UpdateAsync(remoteMatch.EndpointId, normalizedUrl, desiredEvents, cancellationToken);
            outcome = "Updated";
        }
        else
        {
            mutationResult = WebhookEndpointMutationResult.Success(remoteMatch);
            outcome = "Synced";
        }

        if (!mutationResult.Succeeded || mutationResult.Endpoint is null)
        {
            return Result<EnsureHeyGenWebhookEndpointResponse>.Failure(
                "heygen.webhook.ensure.failed",
                mutationResult.FailureReason);
        }

        var registration = new VideoWebhookEndpointRegistration(
            mutationResult.Endpoint.ProviderName,
            mutationResult.Endpoint.EndpointId,
            mutationResult.Endpoint.Url,
            mutationResult.Endpoint.Status,
            mutationResult.Endpoint.Events,
            ResolveSecret(mutationResult.Endpoint, localRegistration),
            localRegistration?.CreatedUtc ?? mutationResult.Endpoint.CreatedUtc ?? _clock.UtcNow,
            _clock.UtcNow);

        await _registrationRepository.SaveAsync(registration, cancellationToken);

        return Result<EnsureHeyGenWebhookEndpointResponse>.Success(
            new EnsureHeyGenWebhookEndpointResponse(
                outcome,
                registration.ProviderName,
                registration.EndpointId,
                registration.Url,
                registration.Status,
                registration.Events,
                !string.IsNullOrWhiteSpace(registration.Secret)));
    }

    private static WebhookEndpointDescriptor? ResolveRemoteMatch(
        IReadOnlyList<WebhookEndpointDescriptor> endpoints,
        VideoWebhookEndpointRegistration? localRegistration,
        string normalizedUrl)
    {
        if (localRegistration is not null)
        {
            var byEndpointId = endpoints.FirstOrDefault(endpoint =>
                endpoint.ProviderName.Equals("HeyGen", StringComparison.OrdinalIgnoreCase) &&
                endpoint.EndpointId.Equals(localRegistration.EndpointId, StringComparison.Ordinal));
            if (byEndpointId is not null)
            {
                return byEndpointId;
            }
        }

        return endpoints.FirstOrDefault(endpoint =>
            endpoint.ProviderName.Equals("HeyGen", StringComparison.OrdinalIgnoreCase) &&
            endpoint.Url.Equals(normalizedUrl, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEndpointUpToDate(
        WebhookEndpointDescriptor endpoint,
        string normalizedUrl,
        IReadOnlyList<string> desiredEvents) =>
        endpoint.Url.Equals(normalizedUrl, StringComparison.OrdinalIgnoreCase) &&
        endpoint.Events.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(desiredEvents.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> NormalizeEvents(IReadOnlyList<string>? events) =>
        (events is null || events.Count == 0 ? DefaultEvents : events)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string ResolveSecret(WebhookEndpointDescriptor endpoint, VideoWebhookEndpointRegistration? localRegistration) =>
        !string.IsNullOrWhiteSpace(endpoint.Secret)
            ? endpoint.Secret
            : localRegistration?.Secret ?? string.Empty;

    private string? ResolveWebhookUrl(string? requestedUrl)
    {
        if (!string.IsNullOrWhiteSpace(requestedUrl))
        {
            return requestedUrl.Trim();
        }

        return _publicWebhookUrlResolver?.ResolveHeyGenWebhookUrl();
    }
}
