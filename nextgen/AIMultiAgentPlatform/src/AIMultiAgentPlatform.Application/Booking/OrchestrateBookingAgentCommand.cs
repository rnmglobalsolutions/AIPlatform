using AIMultiAgentPlatform.Contracts.Booking;

namespace AIMultiAgentPlatform.Application.Booking;

public sealed record OrchestrateBookingAgentCommand(
    OrchestrateBookingAgentRequest Request,
    string CorrelationId = "");
