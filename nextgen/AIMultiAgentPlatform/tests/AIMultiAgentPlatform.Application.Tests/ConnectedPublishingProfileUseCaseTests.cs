using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Application.Publishing;
using AIMultiAgentPlatform.Contracts.Publishing;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class ConnectedPublishingProfileUseCaseTests
{
    [Fact]
    public async Task UpsertConnectedPublishingProfile_StoresSecretAndPersistsReference()
    {
        var tenant = CreateTenant();
        var repository = new FakeConnectedPublishingProfileRepository();
        var secretStore = new FakePublishingSecretStore();
        var useCase = new UpsertConnectedPublishingProfileUseCase(
            new FakeTenantRepository(tenant),
            repository,
            secretStore,
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new UpsertConnectedPublishingProfileRequest(
                tenant.TenantId.Value,
                "Buffer",
                "Instagram",
                "buffer_profile_123",
                "token_123",
                "RNM Instagram"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("publish_secret_001", result.Value!.AccessTokenSecretReference);
        Assert.True(result.Value.HasAccessTokenSecret);
        Assert.Equal("publish_secret_001", repository.Saved!.AccessTokenSecretReference);
        Assert.Equal("token_123", await secretStore.GetAccessTokenAsync("publish_secret_001", CancellationToken.None));
    }

    [Fact]
    public async Task UpsertConnectedPublishingProfile_AllowsReusingExistingSecretReference()
    {
        var tenant = CreateTenant();
        var repository = new FakeConnectedPublishingProfileRepository();
        var secretStore = new FakePublishingSecretStore();
        await secretStore.SaveAccessTokenAsync(
            new PublishingAccessTokenSecret(
                "existing_secret_123",
                tenant.TenantId.Value,
                "Metricool",
                "Instagram",
                "metricool_token",
                DateTime.UtcNow,
                DateTime.UtcNow),
            CancellationToken.None);
        var useCase = new UpsertConnectedPublishingProfileUseCase(
            new FakeTenantRepository(tenant),
            repository,
            secretStore,
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new UpsertConnectedPublishingProfileRequest(
                tenant.TenantId.Value,
                "Metricool",
                "Instagram",
                "metricool_profile_123",
                null,
                "RNM Instagram Metricool",
                "existing_secret_123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("existing_secret_123", repository.Saved!.AccessTokenSecretReference);
        Assert.Equal("metricool_profile_123", repository.Saved.ExternalProfileId);
    }

    private static Tenant CreateTenant() =>
        Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "Agencies",
                "Growth systems",
                "Founders",
                "Bold",
                "BOOK",
                ["Instagram"],
                ["Low visibility"],
                ["No time"],
                []),
            new DateTime(2026, 03, 27, 12, 0, 0, DateTimeKind.Utc));

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(tenant.TenantId.Value == tenantId ? tenant : null);
    }

    private sealed class FakeConnectedPublishingProfileRepository : IConnectedPublishingProfileRepository
    {
        public ConnectedPublishingProfile? Saved { get; private set; }

        public Task SaveAsync(ConnectedPublishingProfile profile, CancellationToken cancellationToken)
        {
            Saved = profile;
            return Task.CompletedTask;
        }

        public Task<ConnectedPublishingProfile?> FindByTenantAndPlatformAsync(string tenantId, string platform, CancellationToken cancellationToken) =>
            Task.FromResult(Saved is not null && Saved.TenantId.Value == tenantId && Saved.Platform == platform ? Saved : null);

        public Task<ConnectedPublishingProfile?> FindByTenantPlatformAndProviderAsync(string tenantId, string platform, string providerName, CancellationToken cancellationToken) =>
            Task.FromResult(
                Saved is not null &&
                Saved.TenantId.Value == tenantId &&
                Saved.Platform == platform &&
                Saved.ProviderName == providerName
                    ? Saved
                    : null);

        public Task<IReadOnlyList<ConnectedPublishingProfile>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConnectedPublishingProfile>>(Saved is not null && Saved.TenantId.Value == tenantId ? [Saved] : Array.Empty<ConnectedPublishingProfile>());
    }

    private sealed class FakePublishingSecretStore : IPublishingSecretStore
    {
        private readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);

        public Task SaveAccessTokenAsync(PublishingAccessTokenSecret secret, CancellationToken cancellationToken)
        {
            _items[secret.SecretReference] = secret.AccessToken;
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync(string secretReference, CancellationToken cancellationToken) =>
            Task.FromResult(_items.TryGetValue(secretReference, out var token) ? token : null);
    }

    private sealed class DeterministicIdGenerator : IIdGenerator
    {
        private int _sequence;
        public string NewId(string prefix) => $"{prefix}_{++_sequence:000}";
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 03, 27, 15, 0, 0, DateTimeKind.Utc);
    }
}
