namespace AIMultiAgentPlatform.Contracts.Video;

public sealed record ProcessHeyGenWebhookResponse(
    string Outcome,
    string VideoGenerationJobId,
    string Message);
