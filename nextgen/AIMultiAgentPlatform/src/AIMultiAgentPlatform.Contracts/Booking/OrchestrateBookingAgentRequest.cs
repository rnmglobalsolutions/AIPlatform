namespace AIMultiAgentPlatform.Contracts.Booking;

public sealed record OrchestrateBookingAgentRequest(
    string TenantId,
    string ManyChatContactId,
    string Outcome,
    DateTime? AppointmentUtc = null,
    string? CalendlyEventType = null,
    IReadOnlyList<string>? PreferredChannels = null,
    string? CorrelationId = null);
