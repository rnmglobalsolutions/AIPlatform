namespace AIMultiAgentPlatform.Contracts.Booking;

public sealed record OrchestrateBookingAgentResponse(
    string LeadProfileId,
    string BookingRecordId,
    string BookingStatus,
    string CalendlyUrl,
    string? ReminderScheduleId,
    string? FollowUpSequenceId,
    string LeadLifecycleStage,
    string TriggeredFlow,
    IReadOnlyList<string> TagsToAdd,
    IReadOnlyDictionary<string, string> FieldsToUpsert);
