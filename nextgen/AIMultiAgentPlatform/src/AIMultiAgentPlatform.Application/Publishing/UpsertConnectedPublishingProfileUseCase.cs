using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Publishing;
using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Application.Publishing;

public sealed class UpsertConnectedPublishingProfileUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IConnectedPublishingProfileRepository _connectedPublishingProfileRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public UpsertConnectedPublishingProfileUseCase(
        ITenantRepository tenantRepository,
        IConnectedPublishingProfileRepository connectedPublishingProfileRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _connectedPublishingProfileRepository = connectedPublishingProfileRepository;
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
            string.IsNullOrWhiteSpace(request.ExternalProfileId) ||
            string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return Result<UpsertConnectedPublishingProfileResponse>.Failure(
                "publishing.profile.invalid",
                "TenantId, ProviderName, Platform, ExternalProfileId, and AccessToken are required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<UpsertConnectedPublishingProfileResponse>.Failure(
                "publishing.profile.tenant.not-found",
                "Tenant was not found.");
        }

        var existing = await _connectedPublishingProfileRepository.FindByTenantAndPlatformAsync(request.TenantId, request.Platform, cancellationToken);
        var profile = new ConnectedPublishingProfile(
            existing?.ConnectedPublishingProfileId ?? _idGenerator.NewId("publish_profile"),
            tenant.TenantId,
            request.ProviderName.Trim(),
            request.Platform.Trim(),
            request.ExternalProfileId.Trim(),
            request.AccessToken.Trim(),
            string.IsNullOrWhiteSpace(request.DisplayName) ? request.Platform.Trim() : request.DisplayName.Trim(),
            existing?.CreatedUtc ?? _clock.UtcNow,
            _clock.UtcNow);

        await _connectedPublishingProfileRepository.SaveAsync(profile, cancellationToken);

        return Result<UpsertConnectedPublishingProfileResponse>.Success(
            new UpsertConnectedPublishingProfileResponse(
                profile.ConnectedPublishingProfileId,
                profile.ProviderName,
                profile.Platform,
                profile.ExternalProfileId,
                profile.DisplayName));
    }
}
