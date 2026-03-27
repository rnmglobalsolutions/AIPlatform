using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.DailyContent;
using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Application.Orchestration;
using AIMultiAgentPlatform.Application.Publishing;
using AIMultiAgentPlatform.Application.ReviewAndScheduling;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Contracts.Publishing;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class CommandEnqueueUseCaseTests
{
    [Fact]
    public async Task EnqueueProcessTallySubmissionUseCase_QueuesExpectedEnvelope()
    {
        var bus = new FakeCommandEnqueuer();
        var useCase = new EnqueueProcessTallySubmissionUseCase(bus, new DeterministicIdGenerator(), new FixedClock());
        var request = new TallySubmissionRequest(
            "sub_123",
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "B2B consultants");

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PlatformCommandNames.ProcessTallySubmission, result.Value!.CommandName);
        Assert.Equal("cmd_001", result.Value.MessageId);
        Assert.Equal("tally-sub_123", result.Value.CorrelationId);
        Assert.Equal(PlatformCommandNames.ProcessTallySubmission, bus.CommandName);
        Assert.NotNull(bus.Envelope);
        Assert.Equal("cmd_001", bus.Envelope!.MessageId);
        Assert.Equal("tally-sub_123", bus.Envelope.CorrelationId);
        Assert.Equal(string.Empty, bus.Envelope.TenantId);
        Assert.Contains("sub_123", bus.Envelope.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnqueueGenerateDailyContentPackageUseCase_QueuesExpectedEnvelope()
    {
        var bus = new FakeCommandEnqueuer();
        var useCase = new EnqueueGenerateDailyContentPackageUseCase(bus, new DeterministicIdGenerator(), new FixedClock());
        var request = new GenerateDailyContentPackageRequest("tenant_123", "backlog_123", 4);

        var result = await useCase.ExecuteAsync(new GenerateDailyContentPackageCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PlatformCommandNames.GenerateDailyContentPackage, result.Value!.CommandName);
        Assert.Equal("tenant_123", result.Value.TenantId);
        Assert.Equal("daily-tenant_123-4", result.Value.CorrelationId);
        Assert.Equal(PlatformCommandNames.GenerateDailyContentPackage, bus.CommandName);
        Assert.NotNull(bus.Envelope);
        Assert.Equal("tenant_123", bus.Envelope!.TenantId);
        Assert.Equal("4", bus.Envelope.Properties!["sequence"]);
        Assert.Contains("\"tenantId\":\"tenant_123\"", bus.Envelope.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnqueueReviewAndScheduleDailyContentUseCase_QueuesExpectedEnvelope()
    {
        var enqueuer = new FakeCommandEnqueuer();
        var useCase = new EnqueueReviewAndScheduleDailyContentUseCase(enqueuer, new DeterministicIdGenerator(), new FixedClock());
        var request = new ReviewAndScheduleDailyContentRequest("tenant_123", "daily_request_123");

        var result = await useCase.ExecuteAsync(new ReviewAndScheduleDailyContentCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PlatformCommandNames.ReviewAndScheduleDailyContent, result.Value!.CommandName);
        Assert.Equal("review-tenant_123-daily_request_123", result.Value.CorrelationId);
        Assert.Equal(PlatformCommandNames.ReviewAndScheduleDailyContent, enqueuer.CommandName);
        Assert.Equal("tenant_123", enqueuer.Envelope!.TenantId);
    }

    [Fact]
    public async Task EnqueuePublishScheduledContentUseCase_QueuesExpectedEnvelope()
    {
        var enqueuer = new FakeCommandEnqueuer();
        var useCase = new EnqueuePublishScheduledContentUseCase(enqueuer, new DeterministicIdGenerator(), new FixedClock());
        var request = new PublishScheduledContentRequest("tenant_123", "schedule_123");

        var result = await useCase.ExecuteAsync(new PublishScheduledContentCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PlatformCommandNames.PublishScheduledContent, result.Value!.CommandName);
        Assert.Equal("publish-tenant_123-schedule_123", result.Value.CorrelationId);
        Assert.Equal(PlatformCommandNames.PublishScheduledContent, enqueuer.CommandName);
        Assert.Equal("schedule_123", enqueuer.Envelope!.Properties!["schedulingJobId"]);
    }

    private sealed class FakeCommandEnqueuer : ICommandEnqueuer
    {
        public string? CommandName { get; private set; }

        public MessageEnvelope? Envelope { get; private set; }

        public Task EnqueueAsync(string commandName, MessageEnvelope envelope, CancellationToken cancellationToken = default)
        {
            CommandName = commandName;
            Envelope = envelope;
            return Task.CompletedTask;
        }
    }

    private sealed class DeterministicIdGenerator : IIdGenerator
    {
        private int _counter;

        public string NewId(string prefix) => $"{prefix}_{++_counter:000}";
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 03, 27, 14, 00, 00, DateTimeKind.Utc);
    }
}
