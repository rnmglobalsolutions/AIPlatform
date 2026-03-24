using AIMultiAgentPlatform.Domain.Leads;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface ILeadProfileRepository
{
    Task SaveAsync(LeadProfile leadProfile, CancellationToken cancellationToken);

    Task<LeadProfile?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken);
}
