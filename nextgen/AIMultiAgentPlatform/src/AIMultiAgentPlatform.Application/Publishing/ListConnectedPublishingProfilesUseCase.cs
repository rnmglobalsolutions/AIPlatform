using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Publishing;

namespace AIMultiAgentPlatform.Application.Publishing;

public sealed class ListConnectedPublishingProfilesUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IConnectedPublishingProfileRepository _connectedPublishingProfileRepository;

    public ListConnectedPublishingProfilesUseCase(
        ITenantRepository tenantRepository,
        IConnectedPublishingProfileRepository connectedPublishingProfileRepository)
    {
        _tenantRepository = tenantRepository;
        _connectedPublishingProfileRepository = connectedPublishingProfileRepository;
    }

    public async Task<Result<ListConnectedPublishingProfilesResponse>> ExecuteAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<ListConnectedPublishingProfilesResponse>.Failure(
                "publishing.profile.tenant.required",
                "TenantId is required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<ListConnectedPublishingProfilesResponse>.Failure(
                "publishing.profile.tenant.not-found",
                "Tenant was not found.");
        }

        var profiles = await _connectedPublishingProfileRepository.ListByTenantAsync(tenantId, cancellationToken);
        return Result<ListConnectedPublishingProfilesResponse>.Success(
            new ListConnectedPublishingProfilesResponse(
                profiles.Select(profile => new ConnectedPublishingProfileDto(
                    profile.ConnectedPublishingProfileId,
                    profile.ProviderName,
                    profile.Platform,
                    profile.ExternalProfileId,
                    profile.DisplayName,
                    profile.AccessTokenSecretReference,
                    profile.HasAccessTokenSecret)).ToArray()));
    }
}
