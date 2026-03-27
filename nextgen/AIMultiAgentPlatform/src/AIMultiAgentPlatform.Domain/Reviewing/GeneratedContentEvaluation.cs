namespace AIMultiAgentPlatform.Domain.Reviewing;

public sealed record GeneratedContentEvaluation(
    double HookScore,
    double ClarityScore,
    double RelevanceScore,
    double LeadGenerationScore,
    double SpecificityScore,
    double PlatformFitScore,
    double AntiRepetitionScore,
    double OverallScore,
    string Feedback,
    string OptimizedCallToAction,
    IReadOnlyList<string> Warnings);
