using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Booking;

public sealed record BookingRecord(
    string BookingRecordId,
    TenantId TenantId,
    string LeadProfileId,
    string ManyChatContactId,
    BookingStatus Status,
    string CalendlyUrl,
    string EventType,
    DateTime? AppointmentUtc,
    DateTime UpdatedUtc,
    string AttributedPublishedContentRecordId = "",
    string AttributedPlatform = "",
    string AttributedProviderName = "");
