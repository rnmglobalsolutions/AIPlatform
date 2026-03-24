namespace AIMultiAgentPlatform.Contracts.Voice;

public sealed record OrchestrateVoiceAgentResponse(
    string VoiceCallSessionId,
    string ExternalCallId,
    string Objective,
    string CallStatus,
    string CallDisposition,
    string LeadLifecycleStage,
    string TriggeredFlow,
    string? BookingRecordId,
    string? ReminderScheduleId,
    string? FollowUpSequenceId,
    IReadOnlyList<string> TagsToAdd,
    IReadOnlyDictionary<string, string> FieldsToUpsert,
    string TranscriptExcerpt);
