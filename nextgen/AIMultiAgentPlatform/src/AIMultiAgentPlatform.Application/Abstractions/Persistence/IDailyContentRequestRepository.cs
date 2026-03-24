using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IDailyContentRequestRepository
{
    Task SaveAsync(DailyContentRequest request, CancellationToken cancellationToken);

    Task<DailyContentRequest?> FindByIdAsync(string requestId, CancellationToken cancellationToken);
}
