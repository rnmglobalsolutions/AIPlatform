using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Voice;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryVoiceCallSessionRepository : IVoiceCallSessionRepository
{
    private readonly ConcurrentDictionary<string, VoiceCallSession> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(VoiceCallSession voiceCallSession, CancellationToken cancellationToken)
    {
        _items[voiceCallSession.VoiceCallSessionId] = voiceCallSession;
        return Task.CompletedTask;
    }

    public Task<VoiceCallSession?> FindByIdAsync(string voiceCallSessionId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(voiceCallSessionId));

    public VoiceCallSession? Find(string voiceCallSessionId) =>
        _items.TryGetValue(voiceCallSessionId, out var voiceCallSession) ? voiceCallSession : null;

    public IReadOnlyList<VoiceCallSession> ListAll() => _items.Values.ToArray();
}
