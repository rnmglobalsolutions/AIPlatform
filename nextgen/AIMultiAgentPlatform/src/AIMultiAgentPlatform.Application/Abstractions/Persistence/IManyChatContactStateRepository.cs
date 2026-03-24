using AIMultiAgentPlatform.Domain.Leads;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IManyChatContactStateRepository
{
    Task SaveAsync(ManyChatContactState contactState, CancellationToken cancellationToken);

    Task<ManyChatContactState?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken);
}
