using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Reminders;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryReminderScheduleRepository : IReminderScheduleRepository
{
    private readonly ConcurrentDictionary<string, ReminderSchedule> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(ReminderSchedule reminderSchedule, CancellationToken cancellationToken)
    {
        _items[reminderSchedule.BookingRecordId] = reminderSchedule;
        return Task.CompletedTask;
    }

    public Task<ReminderSchedule?> FindByBookingRecordAsync(string bookingRecordId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(bookingRecordId));

    public ReminderSchedule? Find(string bookingRecordId) =>
        _items.TryGetValue(bookingRecordId, out var reminderSchedule) ? reminderSchedule : null;

    public IReadOnlyList<ReminderSchedule> ListAll() => _items.Values.ToArray();
}
