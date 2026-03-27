namespace AIMultiAgentPlatform.Domain.Content;

public sealed record VideoClipExtractionPlan(
    string Label,
    double StartSeconds,
    double EndSeconds,
    string TranscriptExcerpt,
    string Intent);
