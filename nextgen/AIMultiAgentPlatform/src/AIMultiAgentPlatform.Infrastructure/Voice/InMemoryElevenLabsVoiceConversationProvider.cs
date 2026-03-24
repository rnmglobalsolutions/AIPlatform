using AIMultiAgentPlatform.Application.Abstractions.Voice;
using AIMultiAgentPlatform.Domain.Voice;

namespace AIMultiAgentPlatform.Infrastructure.Voice;

public sealed class InMemoryElevenLabsVoiceConversationProvider : IVoiceConversationProvider
{
    public Task<VoiceConversationResult> ExecuteAsync(VoiceConversationRequest request, CancellationToken cancellationToken)
    {
        var startedUtc = DateTime.UtcNow;
        var completedUtc = startedUtc.AddMinutes(3);

        var disposition = request.Objective switch
        {
            VoiceCallObjective.Qualification => CallDisposition.Qualified,
            VoiceCallObjective.Booking => CallDisposition.Booked,
            VoiceCallObjective.Reminder => CallDisposition.ReminderDelivered,
            _ => CallDisposition.FollowUpCompleted
        };

        var transcript = request.Objective switch
        {
            VoiceCallObjective.Qualification => $"Hi {request.LeadName}, this is the AI voice assistant. We confirmed interest and qualified the lead for the next step.",
            VoiceCallObjective.Booking => $"Hi {request.LeadName}, we confirmed availability and booked the strategy call during the voice conversation.",
            VoiceCallObjective.Reminder => $"Hi {request.LeadName}, this is your voice reminder for the upcoming appointment. The lead confirmed attendance.",
            _ => $"Hi {request.LeadName}, this follow-up voice call re-engaged the lead and pushed the conversation forward."
        };

        var summary = request.Objective switch
        {
            VoiceCallObjective.Qualification => "Voice qualification completed successfully and the lead is ready for nurture or booking follow-up.",
            VoiceCallObjective.Booking => "Voice booking call completed successfully and produced a booked appointment.",
            VoiceCallObjective.Reminder => "Voice reminder was delivered and the appointment remains on track.",
            _ => "Voice follow-up completed and kept the lead active in the funnel."
        };

        return Task.FromResult(
            new VoiceConversationResult(
                $"elevenlabs-call-{Guid.NewGuid():N}"[..29],
                VoiceCallStatus.Completed,
                disposition,
                transcript,
                summary,
                startedUtc,
                completedUtc,
                request.Objective == VoiceCallObjective.Booking ? completedUtc.AddDays(2) : null));
    }
}
