using AIMultiAgentPlatform.Domain.FollowUps;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IFollowUpSequenceRepository
{
    Task SaveAsync(FollowUpSequence followUpSequence, CancellationToken cancellationToken);

    Task<FollowUpSequence?> FindByLeadProfileAsync(string leadProfileId, CancellationToken cancellationToken);
}
