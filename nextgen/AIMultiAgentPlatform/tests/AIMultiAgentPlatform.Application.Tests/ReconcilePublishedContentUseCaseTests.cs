using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Application.Publishing;
using AIMultiAgentPlatform.Contracts.Publishing;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class ReconcilePublishedContentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UpdatesPublishedRecordAndStoresMetricSnapshot()
    {
        var tenant = Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "Agencies",
                "Growth retainers",
                "Agency owners",
                "Direct",
                "BOOK",
                ["Instagram"],
                ["Inconsistent pipeline"],
                ["No time"],
                Array.Empty<string>()),
            new DateTime(2026, 03, 27, 12, 0, 0, DateTimeKind.Utc));
        var schedulingJob = new Domain.Publishing.SchedulingJob(
            "schedule_001",
            "daily_request_001",
            tenant.TenantId,
            Domain.Publishing.SchedulingStatus.Published,
            "Published",
            DateTime.UtcNow,
            [new Domain.Publishing.PublicationTarget("Instagram", DateTime.UtcNow, "Payload")]);
        var profile = new ConnectedPublishingProfile(
            "profile_001",
            tenant.TenantId,
            "Buffer",
            "Instagram",
            "external_profile_001",
            "publish_secret_buffer",
            "RNM Instagram",
            DateTime.UtcNow,
            DateTime.UtcNow);
        var record = new PublishedContentRecord(
            "published_001",
            "daily_request_001",
            "schedule_001",
            tenant.TenantId,
            "Buffer",
            "Instagram",
            "external_profile_001",
            "post_123",
            string.Empty,
            "Caption",
            "https://blob.test/video.mp4",
            PublishedContentStatus.Published,
            string.Empty,
            new DateTime(2026, 03, 27, 13, 0, 0, DateTimeKind.Utc));

        var publishedRecordRepository = new FakePublishedContentRecordRepository(record);
        var metricRepository = new FakePublishedContentMetricSnapshotRepository();
        var useCase = new ReconcilePublishedContentUseCase(
            new FakeTenantRepository(tenant),
            new FakeSchedulingJobRepository(schedulingJob),
            new FakeConnectedPublishingProfileRepository([profile]),
            new FakePublishingSecretStore(("publish_secret_buffer", "token_123")),
            publishedRecordRepository,
            metricRepository,
            new FixedPublishingProviderSelector(new FakePublishingProvider()),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new ReconcilePublishedContentRequest(tenant.TenantId.Value, schedulingJob.SchedulingJobId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value!.RecordsProcessed);
        Assert.Equal(1, result.Value.SnapshotsSaved);
        Assert.Equal("service_update_123", publishedRecordRepository.Saved.Single().ExternalUrl);
        Assert.Single(metricRepository.Saved);
        Assert.Equal(1200, metricRepository.Saved[0].Reach);
        Assert.Equal("sent", metricRepository.Saved[0].ProviderStatus);
    }

    [Fact]
    public async Task ExecuteAsync_UsesProviderSpecificConnectedProfileWhenMultipleProvidersSharePlatform()
    {
        var tenant = Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "Agencies",
                "Growth retainers",
                "Agency owners",
                "Direct",
                "BOOK",
                ["Instagram"],
                ["Inconsistent pipeline"],
                ["No time"],
                Array.Empty<string>()),
            new DateTime(2026, 03, 27, 12, 0, 0, DateTimeKind.Utc));
        var schedulingJob = new Domain.Publishing.SchedulingJob(
            "schedule_001",
            "daily_request_001",
            tenant.TenantId,
            Domain.Publishing.SchedulingStatus.Published,
            "Published",
            DateTime.UtcNow,
            [new Domain.Publishing.PublicationTarget("Instagram", DateTime.UtcNow, "Payload")]);
        var bufferProfile = new ConnectedPublishingProfile(
            "profile_buffer",
            tenant.TenantId,
            "Buffer",
            "Instagram",
            "external_profile_buffer",
            "publish_secret_buffer",
            "RNM Instagram Buffer",
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-10));
        var metricoolProfile = new ConnectedPublishingProfile(
            "profile_metricool",
            tenant.TenantId,
            "Metricool",
            "Instagram",
            "external_profile_metricool",
            "publish_secret_metricool",
            "RNM Instagram Metricool",
            DateTime.UtcNow,
            DateTime.UtcNow);
        var record = new PublishedContentRecord(
            "published_001",
            "daily_request_001",
            "schedule_001",
            tenant.TenantId,
            "Buffer",
            "Instagram",
            "external_profile_buffer",
            "post_123",
            string.Empty,
            "Caption",
            "https://blob.test/video.mp4",
            PublishedContentStatus.Published,
            string.Empty,
            new DateTime(2026, 03, 27, 13, 0, 0, DateTimeKind.Utc));

        var publishedRecordRepository = new FakePublishedContentRecordRepository(record);
        var metricRepository = new FakePublishedContentMetricSnapshotRepository();
        var fakeProvider = new CapturingPublishingProvider();
        var useCase = new ReconcilePublishedContentUseCase(
            new FakeTenantRepository(tenant),
            new FakeSchedulingJobRepository(schedulingJob),
            new FakeConnectedPublishingProfileRepository([metricoolProfile, bufferProfile]),
            new FakePublishingSecretStore(
                ("publish_secret_buffer", "buffer_token"),
                ("publish_secret_metricool", "metricool_token")),
            publishedRecordRepository,
            metricRepository,
            new FixedPublishingProviderSelector(fakeProvider),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new ReconcilePublishedContentRequest(tenant.TenantId.Value, schedulingJob.SchedulingJobId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("buffer_token", fakeProvider.CapturedRequest!.AccessToken);
        Assert.Equal("external_profile_buffer", fakeProvider.CapturedRequest.ExternalProfileId);
    }

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(tenant.TenantId.Value == tenantId ? tenant : null);
    }

    private sealed class FakeSchedulingJobRepository(Domain.Publishing.SchedulingJob job) : ISchedulingJobRepository
    {
        public Task SaveAsync(Domain.Publishing.SchedulingJob job, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Domain.Publishing.SchedulingJob?> FindByIdAsync(string schedulingJobId, CancellationToken cancellationToken) =>
            Task.FromResult(job.SchedulingJobId == schedulingJobId ? job : null);
        public Task<Domain.Publishing.SchedulingJob?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult(job.DailyContentRequestId == dailyContentRequestId ? job : null);
    }

    private sealed class FakeConnectedPublishingProfileRepository(IReadOnlyList<ConnectedPublishingProfile> profiles) : IConnectedPublishingProfileRepository
    {
        public Task SaveAsync(ConnectedPublishingProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ConnectedPublishingProfile?> FindByTenantAndPlatformAsync(string tenantId, string platform, CancellationToken cancellationToken) =>
            Task.FromResult(profiles
                .Where(item => item.TenantId.Value == tenantId && item.Platform == platform)
                .OrderByDescending(item => item.UpdatedUtc)
                .FirstOrDefault());
        public Task<ConnectedPublishingProfile?> FindByTenantPlatformAndProviderAsync(string tenantId, string platform, string providerName, CancellationToken cancellationToken) =>
            Task.FromResult(profiles
                .Where(item => item.TenantId.Value == tenantId && item.Platform == platform && item.ProviderName == providerName)
                .OrderByDescending(item => item.UpdatedUtc)
                .FirstOrDefault());
        public Task<IReadOnlyList<ConnectedPublishingProfile>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConnectedPublishingProfile>>(profiles.Where(item => item.TenantId.Value == tenantId).ToArray());
    }

    private sealed class FakePublishingSecretStore(params (string SecretReference, string AccessToken)[] entries) : IPublishingSecretStore
    {
        private readonly Dictionary<string, string> _items = entries.ToDictionary(item => item.SecretReference, item => item.AccessToken, StringComparer.Ordinal);

        public Task SaveAccessTokenAsync(PublishingAccessTokenSecret secret, CancellationToken cancellationToken)
        {
            _items[secret.SecretReference] = secret.AccessToken;
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync(string secretReference, CancellationToken cancellationToken) =>
            Task.FromResult(_items.TryGetValue(secretReference, out var accessToken) ? accessToken : null);
    }

    private sealed class FakePublishedContentRecordRepository(PublishedContentRecord seed) : IPublishedContentRecordRepository
    {
        public List<PublishedContentRecord> Saved { get; } = [seed];

        public Task SaveAsync(PublishedContentRecord record, CancellationToken cancellationToken)
        {
            Saved.RemoveAll(item => item.PublishedContentRecordId == record.PublishedContentRecordId);
            Saved.Add(record);
            return Task.CompletedTask;
        }

        public Task<PublishedContentRecord?> FindByIdAsync(string publishedContentRecordId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.SingleOrDefault(item => item.PublishedContentRecordId == publishedContentRecordId));

        public Task<IReadOnlyList<PublishedContentRecord>> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublishedContentRecord>>(Saved.Where(item => item.DailyContentRequestId == dailyContentRequestId).ToArray());

        public Task<IReadOnlyList<PublishedContentRecord>> FindBySchedulingJobIdAsync(string schedulingJobId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublishedContentRecord>>(Saved.Where(item => item.SchedulingJobId == schedulingJobId).ToArray());
    }

    private sealed class FakePublishedContentMetricSnapshotRepository : IPublishedContentMetricSnapshotRepository
    {
        public List<PublishedContentMetricSnapshot> Saved { get; } = [];

        public Task SaveAsync(PublishedContentMetricSnapshot snapshot, CancellationToken cancellationToken)
        {
            Saved.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PublishedContentMetricSnapshot>> FindByPublishedContentRecordIdAsync(string publishedContentRecordId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublishedContentMetricSnapshot>>(Saved.Where(item => item.PublishedContentRecordId == publishedContentRecordId).ToArray());
    }

    private sealed class FakePublishingProvider : IPublishingProvider
    {
        public string ProviderName => "Buffer";

        public Task<PublishingResult> PublishAsync(PublishingRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(PublishingResult.Success(request.Platform, "post_123", string.Empty));

        public Task<PublishingReconciliationResult> ReconcileAsync(PublishingReconciliationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(
                PublishingReconciliationResult.Success(
                    request.Platform,
                    "sent",
                    "service_update_123",
                    new PublishingMetrics(1200, 42, 8, 3, 2),
                    new DateTime(2026, 03, 27, 14, 0, 0, DateTimeKind.Utc)));
    }

    private sealed class CapturingPublishingProvider : IPublishingProvider
    {
        public PublishingReconciliationRequest? CapturedRequest { get; private set; }
        public string ProviderName => "Buffer";

        public Task<PublishingResult> PublishAsync(PublishingRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(PublishingResult.Success(request.Platform, "post_123", string.Empty));

        public Task<PublishingReconciliationResult> ReconcileAsync(PublishingReconciliationRequest request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult(
                PublishingReconciliationResult.Success(
                    request.Platform,
                    "sent",
                    "service_update_123",
                    new PublishingMetrics(1200, 42, 8, 3, 2),
                    new DateTime(2026, 03, 27, 14, 0, 0, DateTimeKind.Utc)));
        }
    }

    private sealed class FixedPublishingProviderSelector(IPublishingProvider provider) : IPublishingProviderSelector
    {
        public IPublishingProvider? Resolve(string providerName) => provider.ProviderName == providerName ? provider : null;
    }

    private sealed class DeterministicIdGenerator : IIdGenerator
    {
        private int _sequence;
        public string NewId(string prefix) => $"{prefix}_{++_sequence:000}";
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 03, 27, 14, 30, 0, DateTimeKind.Utc);
    }
}
