namespace AIMultiAgentPlatform.Domain.Strategy;

public sealed record StrategicProfile(
    string BusinessName,
    string Niche,
    string Offer,
    string TargetAudience,
    string BrandTone,
    string ContentLanguage,
    string MainGoal,
    string DesiredAction,
    string CallToActionKeyword,
    string CalendlyUrl,
    string WebsiteUrl,
    string PrimaryOutcome,
    string ConversionDestination,
    string LeadGoal,
    StrategyConversionMode ConversionMode,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> PainPoints,
    IReadOnlyList<string> Objections,
    IReadOnlyList<string> AvoidTopics,
    ContentPlanTier ContentPlanTier = ContentPlanTier.Starter);
