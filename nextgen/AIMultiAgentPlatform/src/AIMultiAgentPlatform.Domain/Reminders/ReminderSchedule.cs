using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Reminders;

public sealed record ReminderSchedule(
    string ReminderScheduleId,
    TenantId TenantId,
    string BookingRecordId,
    ReminderScheduleStatus Status,
    IReadOnlyList<ReminderTouch> Touches,
    DateTime CreatedUtc);
