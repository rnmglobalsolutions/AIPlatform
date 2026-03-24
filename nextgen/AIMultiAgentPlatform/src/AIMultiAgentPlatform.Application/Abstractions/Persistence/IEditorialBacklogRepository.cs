using AIMultiAgentPlatform.Domain.Editorial;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IEditorialBacklogRepository
{
    Task SaveAsync(EditorialBacklog backlog, CancellationToken cancellationToken);

    Task<EditorialBacklog?> FindByIdAsync(string backlogId, CancellationToken cancellationToken);
}
