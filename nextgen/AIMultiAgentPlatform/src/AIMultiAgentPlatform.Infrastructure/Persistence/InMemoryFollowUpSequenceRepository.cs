using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.FollowUps;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryFollowUpSequenceRepository : IFollowUpSequenceRepository
{
    private readonly ConcurrentDictionary<string, FollowUpSequence> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(FollowUpSequence followUpSequence, CancellationToken cancellationToken)
    {
        _items[followUpSequence.LeadProfileId] = followUpSequence;
        return Task.CompletedTask;
    }

    public Task<FollowUpSequence?> FindByLeadProfileAsync(string leadProfileId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(leadProfileId));

    public FollowUpSequence? Find(string leadProfileId) =>
        _items.TryGetValue(leadProfileId, out var followUpSequence) ? followUpSequence : null;

    public IReadOnlyList<FollowUpSequence> ListAll() => _items.Values.ToArray();
}
