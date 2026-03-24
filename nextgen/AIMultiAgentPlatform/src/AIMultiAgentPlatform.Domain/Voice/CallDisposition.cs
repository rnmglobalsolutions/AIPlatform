namespace AIMultiAgentPlatform.Domain.Voice;

public enum CallDisposition
{
    Qualified = 1,
    BookingRequested = 2,
    Booked = 3,
    ReminderDelivered = 4,
    FollowUpCompleted = 5,
    NoAnswer = 6,
    NeedsHumanFollowUp = 7
}
