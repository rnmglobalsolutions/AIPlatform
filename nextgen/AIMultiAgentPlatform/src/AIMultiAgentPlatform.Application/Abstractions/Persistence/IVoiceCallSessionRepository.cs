using AIMultiAgentPlatform.Domain.Voice;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IVoiceCallSessionRepository
{
    Task SaveAsync(VoiceCallSession voiceCallSession, CancellationToken cancellationToken);

    Task<VoiceCallSession?> FindByIdAsync(string voiceCallSessionId, CancellationToken cancellationToken);
}
