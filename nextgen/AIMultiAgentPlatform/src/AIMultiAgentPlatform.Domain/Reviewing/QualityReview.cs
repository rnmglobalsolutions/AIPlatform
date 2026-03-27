using AIMultiAgentPlatform.Domain.Common;

namespace AIMultiAgentPlatform.Domain.Reviewing;

public sealed record QualityReview(
    string QualityReviewId,
    string DailyContentRequestId,
    TenantId TenantId,
    double HookScore,
    double ClarityScore,
    double RelevanceScore,
    double LeadGenerationScore,
    double OverallScore,
    string Feedback,
    string OptimizedCallToAction,
    DateTime ReviewedUtc,
    double SpecificityScore = 0,
    double PlatformFitScore = 0,
    double AntiRepetitionScore = 0,
    IReadOnlyList<string>? EvaluationWarnings = null);
