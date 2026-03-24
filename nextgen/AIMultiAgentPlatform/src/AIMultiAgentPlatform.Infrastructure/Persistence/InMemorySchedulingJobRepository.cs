using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemorySchedulingJobRepository : ISchedulingJobRepository
{
    private readonly ConcurrentDictionary<string, SchedulingJob> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(SchedulingJob job, CancellationToken cancellationToken)
    {
        _items[job.SchedulingJobId] = job;
        return Task.CompletedTask;
    }

    public Task<SchedulingJob?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == requestId));

    public SchedulingJob? Find(string schedulingJobId) => _items.TryGetValue(schedulingJobId, out var job) ? job : null;

    public IReadOnlyList<SchedulingJob> ListAll() => _items.Values.ToArray();
}
