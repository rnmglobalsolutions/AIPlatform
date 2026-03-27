using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Video;
using AIMultiAgentPlatform.Contracts.Video;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class HeyGenWebhookEndpointManagementUseCaseTests
{
    [Fact]
    public async Task EnsureAsync_CreatesEndpointAndStoresSecret()
    {
        var repository = new FakeVideoWebhookEndpointRegistrationRepository();
        var manager = new FakeWebhookEndpointManager
        {
            ListResult = WebhookEndpointListResult.Success([]),
            CreateResult = WebhookEndpointMutationResult.Success(
                new WebhookEndpointDescriptor(
                    "HeyGen",
                    "endpoint_123",
                    "https://api.test/heygen/webhook",
                    "enabled",
                    ["avatar_video.success"],
                    "secret_123",
                    new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc)))
        };

        var useCase = new EnsureHeyGenWebhookEndpointUseCase(manager, repository, new FixedClock());
        var result = await useCase.ExecuteAsync(
            new EnsureHeyGenWebhookEndpointRequest("https://api.test/heygen/webhook", ["avatar_video.success"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Created", result.Value!.Outcome);
        Assert.Equal("secret_123", repository.Saved!.Secret);
    }

    [Fact]
    public async Task EnsureAsync_ResolvesWebhookUrlWhenRequestDoesNotProvideOne()
    {
        var repository = new FakeVideoWebhookEndpointRegistrationRepository();
        var manager = new FakeWebhookEndpointManager
        {
            ListResult = WebhookEndpointListResult.Success([]),
            CreateResult = WebhookEndpointMutationResult.Success(
                new WebhookEndpointDescriptor(
                    "HeyGen",
                    "endpoint_auto",
                    "https://func.test/api/integrations/heygen/webhook",
                    "enabled",
                    ["avatar_video.success"],
                    "secret_auto",
                    new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc)))
        };

        var useCase = new EnsureHeyGenWebhookEndpointUseCase(
            manager,
            repository,
            new FixedClock(),
            new FixedPublicWebhookUrlResolver("https://func.test/api/integrations/heygen/webhook"));

        var result = await useCase.ExecuteAsync(
            new EnsureHeyGenWebhookEndpointRequest(Events: ["avatar_video.success"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Created", result.Value!.Outcome);
        Assert.Equal("https://func.test/api/integrations/heygen/webhook", repository.Saved!.Url);
    }

    [Fact]
    public async Task EnsureAsync_UpdatesExistingEndpointWhenEventsDrift()
    {
        var repository = new FakeVideoWebhookEndpointRegistrationRepository
        {
            Saved = new VideoWebhookEndpointRegistration(
                "HeyGen",
                "endpoint_456",
                "https://api.test/heygen/webhook",
                "enabled",
                ["avatar_video.success"],
                "secret_456",
                new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 03, 26, 12, 5, 0, DateTimeKind.Utc))
        };
        var manager = new FakeWebhookEndpointManager
        {
            ListResult = WebhookEndpointListResult.Success(
                [
                    new WebhookEndpointDescriptor(
                        "HeyGen",
                        "endpoint_456",
                        "https://api.test/heygen/webhook",
                        "enabled",
                        ["avatar_video.success"],
                        string.Empty,
                        new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc))
                ]),
            UpdateResult = WebhookEndpointMutationResult.Success(
                new WebhookEndpointDescriptor(
                    "HeyGen",
                    "endpoint_456",
                    "https://api.test/heygen/webhook",
                    "enabled",
                    ["avatar_video.success", "avatar_video.failed"],
                    string.Empty,
                    new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc)))
        };

        var useCase = new EnsureHeyGenWebhookEndpointUseCase(manager, repository, new FixedClock());
        var result = await useCase.ExecuteAsync(
            new EnsureHeyGenWebhookEndpointRequest("https://api.test/heygen/webhook", ["avatar_video.success", "avatar_video.failed"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated", result.Value!.Outcome);
        Assert.Equal("secret_456", repository.Saved!.Secret);
    }

    [Fact]
    public async Task DeleteAsync_RemovesLocalRegistrationAfterRemoteDeletion()
    {
        var repository = new FakeVideoWebhookEndpointRegistrationRepository
        {
            Saved = new VideoWebhookEndpointRegistration(
                "HeyGen",
                "endpoint_789",
                "https://api.test/heygen/webhook",
                "enabled",
                ["avatar_video.success"],
                "secret_789",
                new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 03, 26, 12, 5, 0, DateTimeKind.Utc))
        };
        var manager = new FakeWebhookEndpointManager
        {
            DeleteResult = WebhookEndpointDeletionResult.Success()
        };

        var useCase = new DeleteHeyGenWebhookEndpointUseCase(manager, repository);
        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(repository.Saved);
        Assert.Equal("endpoint_789", manager.DeletedEndpointId);
    }

    private sealed class FakeWebhookEndpointManager : IWebhookEndpointManager
    {
        public WebhookEndpointListResult ListResult { get; init; } = WebhookEndpointListResult.Success([]);
        public WebhookEndpointMutationResult CreateResult { get; init; } = WebhookEndpointMutationResult.Failure("not-configured");
        public WebhookEndpointMutationResult UpdateResult { get; init; } = WebhookEndpointMutationResult.Failure("not-configured");
        public WebhookEndpointDeletionResult DeleteResult { get; init; } = WebhookEndpointDeletionResult.Failure("not-configured");
        public string? DeletedEndpointId { get; private set; }

        public Task<WebhookEndpointListResult> ListAsync(CancellationToken cancellationToken) => Task.FromResult(ListResult);

        public Task<WebhookEndpointMutationResult> CreateAsync(string url, IReadOnlyList<string> events, CancellationToken cancellationToken) =>
            Task.FromResult(CreateResult);

        public Task<WebhookEndpointMutationResult> UpdateAsync(string endpointId, string url, IReadOnlyList<string> events, CancellationToken cancellationToken) =>
            Task.FromResult(UpdateResult);

        public Task<WebhookEndpointDeletionResult> DeleteAsync(string endpointId, CancellationToken cancellationToken)
        {
            DeletedEndpointId = endpointId;
            return Task.FromResult(DeleteResult);
        }
    }

    private sealed class FakeVideoWebhookEndpointRegistrationRepository : IVideoWebhookEndpointRegistrationRepository
    {
        public VideoWebhookEndpointRegistration? Saved { get; set; }

        public Task SaveAsync(VideoWebhookEndpointRegistration registration, CancellationToken cancellationToken)
        {
            Saved = registration;
            return Task.CompletedTask;
        }

        public Task<VideoWebhookEndpointRegistration?> FindByProviderAsync(string providerName, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.ProviderName == providerName ? Saved : null);

        public Task DeleteAsync(string providerName, CancellationToken cancellationToken)
        {
            if (Saved?.ProviderName == providerName)
            {
                Saved = null;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FixedPublicWebhookUrlResolver(string resolvedUrl) : IPublicWebhookUrlResolver
    {
        public string? ResolveHeyGenWebhookUrl() => resolvedUrl;
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 03, 26, 12, 30, 0, DateTimeKind.Utc);
    }
}
