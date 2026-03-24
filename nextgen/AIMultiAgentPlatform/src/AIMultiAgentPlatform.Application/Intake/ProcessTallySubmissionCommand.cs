using AIMultiAgentPlatform.Contracts.Intake;

namespace AIMultiAgentPlatform.Application.Intake;

public sealed record ProcessTallySubmissionCommand(TallySubmissionRequest Submission, string CorrelationId = "");
