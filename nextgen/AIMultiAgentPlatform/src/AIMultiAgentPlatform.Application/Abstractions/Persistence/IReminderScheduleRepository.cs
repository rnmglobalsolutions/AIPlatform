using AIMultiAgentPlatform.Domain.Reminders;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IReminderScheduleRepository
{
    Task SaveAsync(ReminderSchedule reminderSchedule, CancellationToken cancellationToken);

    Task<ReminderSchedule?> FindByBookingRecordAsync(string bookingRecordId, CancellationToken cancellationToken);
}
