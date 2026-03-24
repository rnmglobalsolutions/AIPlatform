namespace AIMultiAgentPlatform.Domain.Reviewing;

public sealed record ComplianceIssue(
    string Code,
    string Description,
    string RecommendedFix);
