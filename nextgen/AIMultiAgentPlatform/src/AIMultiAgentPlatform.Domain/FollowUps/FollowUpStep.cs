using AIMultiAgentPlatform.Domain.Communications;

namespace AIMultiAgentPlatform.Domain.FollowUps;

public sealed record FollowUpStep(
    CommunicationChannel Channel,
    DateTime ScheduledUtc,
    string TemplateKey);
