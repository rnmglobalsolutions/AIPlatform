using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Booking;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryBookingRecordRepository : IBookingRecordRepository
{
    private readonly ConcurrentDictionary<string, BookingRecord> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(BookingRecord bookingRecord, CancellationToken cancellationToken)
    {
        _items[BuildKey(bookingRecord.TenantId.Value, bookingRecord.ManyChatContactId)] = bookingRecord;
        return Task.CompletedTask;
    }

    public Task<BookingRecord?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(tenantId, manyChatContactId));

    public BookingRecord? Find(string tenantId, string manyChatContactId) =>
        _items.TryGetValue(BuildKey(tenantId, manyChatContactId), out var bookingRecord) ? bookingRecord : null;

    public IReadOnlyList<BookingRecord> ListAll() => _items.Values.ToArray();

    private static string BuildKey(string tenantId, string manyChatContactId) => $"{tenantId}::{manyChatContactId}";
}
