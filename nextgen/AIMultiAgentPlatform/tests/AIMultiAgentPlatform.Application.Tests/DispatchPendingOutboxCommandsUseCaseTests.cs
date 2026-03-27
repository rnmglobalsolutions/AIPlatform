using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Orchestration;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class DispatchPendingOutboxCommandsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_DispatchesPendingCommandsAndMarksThemDispatched()
    {
        var repository = new FakeOutboxMessageRepository(
            new PendingOutboxCommand(
                "outbox_001",
                PlatformCommandNames.GenerateDailyContentPackage,
                new MessageEnvelope(
                    "msg_001",
                    "corr_001",
                    "tenant_001",
                    "GenerateDailyContentPackageCommand",
                    """{"request":{"tenantId":"tenant_001"}}""",
                    new DateTime(2026, 03, 27, 14, 0, 0, DateTimeKind.Utc)),
                new DateTime(2026, 03, 27, 14, 0, 0, DateTimeKind.Utc)));
        var bus = new FakeCommandBus();
        var useCase = new DispatchPendingOutboxCommandsUseCase(repository, bus, new FixedClock());

        var result = await useCase.ExecuteAsync(10, CancellationToken.None);

        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(1, result.DispatchedCount);
        Assert.Equal(PlatformCommandNames.GenerateDailyContentPackage, bus.CommandName);
        Assert.Equal("outbox_001", repository.DispatchedOutboxMessageId);
    }

    private sealed class FakeOutboxMessageRepository(params PendingOutboxCommand[] pending) : IOutboxMessageRepository
    {
        private readonly List<PendingOutboxCommand> _pending = [.. pending];

        public string? DispatchedOutboxMessageId { get; private set; }

        public Task SaveAsync(PendingOutboxCommand command, CancellationToken cancellationToken)
        {
            _pending.Add(command);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PendingOutboxCommand>> GetPendingAsync(int maxCount, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PendingOutboxCommand>>(_pending.Take(maxCount).ToArray());

        public Task MarkDispatchedAsync(string outboxMessageId, DateTime dispatchedUtc, CancellationToken cancellationToken)
        {
            DispatchedOutboxMessageId = outboxMessageId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCommandBus : ICommandBus
    {
        public string? CommandName { get; private set; }

        public Task SendAsync(string commandName, MessageEnvelope envelope, CancellationToken cancellationToken = default)
        {
            CommandName = commandName;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 03, 27, 14, 5, 0, DateTimeKind.Utc);
    }
}
