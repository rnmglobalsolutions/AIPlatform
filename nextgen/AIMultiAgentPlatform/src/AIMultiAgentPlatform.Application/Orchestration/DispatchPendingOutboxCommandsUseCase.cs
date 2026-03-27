using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;

namespace AIMultiAgentPlatform.Application.Orchestration;

public sealed class DispatchPendingOutboxCommandsUseCase
{
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly ICommandBus _commandBus;
    private readonly IClock _clock;

    public DispatchPendingOutboxCommandsUseCase(
        IOutboxMessageRepository outboxMessageRepository,
        ICommandBus commandBus,
        IClock clock)
    {
        _outboxMessageRepository = outboxMessageRepository;
        _commandBus = commandBus;
        _clock = clock;
    }

    public async Task<DispatchOutboxCommandsResult> ExecuteAsync(
        int maxCount,
        CancellationToken cancellationToken)
    {
        var pending = await _outboxMessageRepository.GetPendingAsync(maxCount, cancellationToken);
        var dispatched = 0;

        foreach (var command in pending)
        {
            await _commandBus.SendAsync(command.CommandName, command.Envelope, cancellationToken);
            await _outboxMessageRepository.MarkDispatchedAsync(command.OutboxMessageId, _clock.UtcNow, cancellationToken);
            dispatched++;
        }

        return new DispatchOutboxCommandsResult(pending.Count, dispatched);
    }
}
