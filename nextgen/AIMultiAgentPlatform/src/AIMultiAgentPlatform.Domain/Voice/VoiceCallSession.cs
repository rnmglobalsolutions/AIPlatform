using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Voice;

public sealed record VoiceCallSession(
    string VoiceCallSessionId,
    TenantId TenantId,
    string LeadProfileId,
    string ManyChatContactId,
    VoiceCallObjective Objective,
    VoiceCallStatus Status,
    CallDisposition Disposition,
    string PhoneNumber,
    string ExternalCallId,
    string Transcript,
    string Summary,
    DateTime StartedUtc,
    DateTime CompletedUtc);
