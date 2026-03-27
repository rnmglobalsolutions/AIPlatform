using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IVideoGenerationJobRepository
{
    Task SaveAsync(VideoGenerationJob job, CancellationToken cancellationToken);

    Task<VideoGenerationJob?> FindByIdAsync(string videoGenerationJobId, CancellationToken cancellationToken);

    Task<VideoGenerationJob?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken);

    Task<VideoGenerationJob?> FindByProviderJobIdAsync(string providerJobId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VideoGenerationJob>> ListActiveAsync(int maxCount, CancellationToken cancellationToken);
}
