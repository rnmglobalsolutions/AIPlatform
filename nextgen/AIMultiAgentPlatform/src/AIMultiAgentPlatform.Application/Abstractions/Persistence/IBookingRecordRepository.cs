using AIMultiAgentPlatform.Domain.Booking;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IBookingRecordRepository
{
    Task SaveAsync(BookingRecord bookingRecord, CancellationToken cancellationToken);

    Task<BookingRecord?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken);
}
