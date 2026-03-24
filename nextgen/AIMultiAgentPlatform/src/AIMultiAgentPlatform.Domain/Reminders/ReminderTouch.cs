using AIMultiAgentPlatform.Domain.Communications;

namespace AIMultiAgentPlatform.Domain.Reminders;

public sealed record ReminderTouch(
    CommunicationChannel Channel,
    DateTime ScheduledUtc,
    string TemplateKey);
