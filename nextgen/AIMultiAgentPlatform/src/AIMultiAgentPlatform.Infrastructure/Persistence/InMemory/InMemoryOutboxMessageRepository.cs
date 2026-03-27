using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryOutboxMessageRepository : IOutboxMessageRepository
{
    private readonly Dictionary<string, PendingOutboxCommand> _commands = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dispatched = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public Task SaveAsync(PendingOutboxCommand command, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _commands[command.OutboxMessageId] = command;
            _dispatched.Remove(command.OutboxMessageId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PendingOutboxCommand>> GetPendingAsync(int maxCount, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<PendingOutboxCommand>>(
                _commands.Values
                    .Where(command => !_dispatched.Contains(command.OutboxMessageId))
                    .OrderBy(command => command.CreatedUtc)
                    .Take(maxCount)
                    .ToArray());
        }
    }

    public Task MarkDispatchedAsync(string outboxMessageId, DateTime dispatchedUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _dispatched.Add(outboxMessageId);
        }

        return Task.CompletedTask;
    }
}
