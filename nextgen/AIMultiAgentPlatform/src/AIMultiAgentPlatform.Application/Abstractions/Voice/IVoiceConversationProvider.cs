using AIMultiAgentPlatform.Domain.Voice;

namespace AIMultiAgentPlatform.Application.Abstractions.Voice;

public interface IVoiceConversationProvider
{
    Task<VoiceConversationResult> ExecuteAsync(VoiceConversationRequest request, CancellationToken cancellationToken);
}

public sealed record VoiceConversationRequest(
    string TenantId,
    string ManyChatContactId,
    string LeadProfileId,
    string LeadName,
    string PhoneNumber,
    VoiceCallObjective Objective,
    string PromptSummary,
    string? PreferredVoiceId);

public sealed record VoiceConversationResult(
    string ExternalCallId,
    VoiceCallStatus Status,
    CallDisposition Disposition,
    string Transcript,
    string Summary,
    DateTime StartedUtc,
    DateTime CompletedUtc,
    DateTime? AppointmentUtc = null);
