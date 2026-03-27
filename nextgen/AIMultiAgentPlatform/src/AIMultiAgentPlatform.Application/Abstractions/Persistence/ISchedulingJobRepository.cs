using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface ISchedulingJobRepository
{
    Task SaveAsync(SchedulingJob job, CancellationToken cancellationToken);

    Task<SchedulingJob?> FindByIdAsync(string schedulingJobId, CancellationToken cancellationToken);

    Task<SchedulingJob?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken);
}
