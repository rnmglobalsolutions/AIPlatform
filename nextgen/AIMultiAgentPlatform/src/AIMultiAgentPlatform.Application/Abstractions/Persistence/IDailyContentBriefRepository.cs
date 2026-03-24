using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IDailyContentBriefRepository
{
    Task SaveAsync(DailyContentBrief brief, CancellationToken cancellationToken);

    Task<DailyContentBrief?> FindByIdAsync(string briefId, CancellationToken cancellationToken);

    Task<DailyContentBrief?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken);
}
