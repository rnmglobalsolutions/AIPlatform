using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Publishing;
using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Application.Publishing;

public sealed class UpsertConnectedPublishingProfileUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IConnectedPublishingProfileRepository _connectedPublishingProfileRepository;
    private readonly IPublishingSecretStore _publishingSecretStore;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public UpsertConnectedPublishingProfileUseCase(
        ITenantRepository tenantRepository,
        IConnectedPublishingProfileRepository connectedPublishingProfileRepository,
        IPublishingSecretStore publishingSecretStore,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _connectedPublishingProfileRepository = connectedPublishingProfileRepository;
        _publishingSecretStore = publishingSecretStore;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<UpsertConnectedPublishingProfileResponse>> ExecuteAsync(
        UpsertConnectedPublishingProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) ||
            string.IsNullOrWhiteSpace(request.ProviderName) ||
            string.IsNullOrWhiteSpace(request.Platform) ||
            string.IsNullOrWhiteSpace(request.ExternalProfileId))
        {
            return Result<UpsertConnectedPublishingProfileResponse>.Failure(
                "publishing.profile.invalid",
                "TenantId, ProviderName, Platform, and ExternalProfileId are required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<UpsertConnectedPublishingProfileResponse>.Failure(
                "publishing.profile.tenant.not-found",
                "Tenant was not found.");
        }

        var normalizedProviderName = request.ProviderName.Trim();
        var normalizedPlatform = request.Platform.Trim();
        var normalizedExternalProfileId = request.ExternalProfileId.Trim();
        var existing = await _connectedPublishingProfileRepository.FindByTenantPlatformAndProviderAsync(
            request.TenantId,
            normalizedPlatform,
            normalizedProviderName,
            cancellationToken);
        var normalizedSecretReference = string.IsNullOrWhiteSpace(request.AccessTokenSecretReference)
            ? existing?.AccessTokenSecretReference ?? _idGenerator.NewId("publish_secret")
            : request.AccessTokenSecretReference.Trim();
        var normalizedAccessToken = request.AccessToken?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedAccessToken))
        {
            if (string.IsNullOrWhiteSpace(normalizedSecretReference))
            {
                return Result<UpsertConnectedPublishingProfileResponse>.Failure(
                    "publishing.profile.secret.required",
                    "AccessToken or AccessTokenSecretReference is required.");
            }

            var existingSecret = await _publishingSecretStore.GetAccessTokenAsync(normalizedSecretReference, cancellationToken);
            if (string.IsNullOrWhiteSpace(existingSecret))
            {
                return Result<UpsertConnectedPublishingProfileResponse>.Failure(
                    "publishing.profile.secret.not-found",
                    "The requested access token secret reference was not found.");
            }
        }
        else
        {
            await _publishingSecretStore.SaveAccessTokenAsync(
                new PublishingAccessTokenSecret(
                    normalizedSecretReference,
                    tenant.TenantId.Value,
                    normalizedProviderName,
                    normalizedPlatform,
                    normalizedAccessToken,
                    existing?.CreatedUtc ?? _clock.UtcNow,
                    _clock.UtcNow),
                cancellationToken);
        }

        var profile = new ConnectedPublishingProfile(
            existing?.ConnectedPublishingProfileId ?? _idGenerator.NewId("publish_profile"),
            tenant.TenantId,
            normalizedProviderName,
            normalizedPlatform,
            normalizedExternalProfileId,
            normalizedSecretReference,
            string.IsNullOrWhiteSpace(request.DisplayName) ? normalizedPlatform : request.DisplayName.Trim(),
            existing?.CreatedUtc ?? _clock.UtcNow,
            _clock.UtcNow);

        await _connectedPublishingProfileRepository.SaveAsync(profile, cancellationToken);

        return Result<UpsertConnectedPublishingProfileResponse>.Success(
            new UpsertConnectedPublishingProfileResponse(
                profile.ConnectedPublishingProfileId,
                profile.ProviderName,
                profile.Platform,
                profile.ExternalProfileId,
                profile.DisplayName,
                profile.AccessTokenSecretReference,
                profile.HasAccessTokenSecret));
    }
}
