using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryVideoGenerationJobRepository : IVideoGenerationJobRepository
{
    private readonly ConcurrentDictionary<string, VideoGenerationJob> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(VideoGenerationJob job, CancellationToken cancellationToken)
    {
        _items[job.VideoGenerationJobId] = job;
        return Task.CompletedTask;
    }

    public Task<VideoGenerationJob?> FindByIdAsync(string videoGenerationJobId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(videoGenerationJobId));

    public Task<VideoGenerationJob?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == dailyContentRequestId));

    public Task<VideoGenerationJob?> FindByProviderJobIdAsync(string providerJobId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.ProviderJobId == providerJobId));

    public Task<IReadOnlyList<VideoGenerationJob>> ListActiveAsync(int maxCount, CancellationToken cancellationToken)
    {
        var take = maxCount <= 0 ? 10 : maxCount;
        var items = _items.Values
            .Where(item => item.Status is VideoGenerationJobStatus.Submitted or VideoGenerationJobStatus.Processing)
            .OrderBy(item => item.RequestedUtc)
            .Take(take)
            .ToArray();

        return Task.FromResult<IReadOnlyList<VideoGenerationJob>>(items);
    }

    public VideoGenerationJob? Find(string videoGenerationJobId) =>
        _items.TryGetValue(videoGenerationJobId, out var job) ? job : null;

    public IReadOnlyList<VideoGenerationJob> ListAll() => _items.Values.ToArray();
}
