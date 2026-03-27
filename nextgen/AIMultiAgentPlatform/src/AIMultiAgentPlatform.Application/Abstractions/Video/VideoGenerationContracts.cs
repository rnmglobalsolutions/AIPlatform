namespace AIMultiAgentPlatform.Application.Abstractions.Video;

public sealed record VideoGenerationRequest(
    string TenantId,
    string CorrelationId,
    string ProviderProfile,
    string Title,
    string Script,
    string Language,
    string AspectRatio,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record VideoGenerationSubmissionResult(
    bool Submitted,
    string ProviderJobId,
    string ProviderName,
    string Status,
    string FailureReason)
{
    public static VideoGenerationSubmissionResult Accepted(
        string providerJobId,
        string providerName,
        string status = "Submitted") =>
        new(true, providerJobId, providerName, status, string.Empty);

    public static VideoGenerationSubmissionResult Rejected(
        string providerName,
        string failureReason) =>
        new(false, string.Empty, providerName, "Rejected", failureReason);
}

public sealed record VideoGenerationStatusResult(
    string ProviderJobId,
    string ProviderName,
    string Status,
    string VideoDownloadUrl,
    string TranscriptText,
    string FailureReason,
    IReadOnlyList<AIMultiAgentPlatform.Domain.Content.TimedTranscriptSegment>? TranscriptSegments = null);
