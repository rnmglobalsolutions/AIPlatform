namespace AIMultiAgentPlatform.Domain.Content;

public sealed record TimedTranscriptSegment(
    double StartSeconds,
    double EndSeconds,
    string Text);
