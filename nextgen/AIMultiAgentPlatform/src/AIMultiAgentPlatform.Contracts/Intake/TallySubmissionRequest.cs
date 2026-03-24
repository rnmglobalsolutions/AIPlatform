namespace AIMultiAgentPlatform.Contracts.Intake;

public sealed record TallySubmissionRequest(
    string ExternalSubmissionId,
    string BusinessName,
    string PrimaryContactName,
    string PrimaryContactEmail,
    string Niche,
    string Offer,
    string TargetAudience,
    string BrandTone,
    string CallToActionKeyword,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> PainPoints,
    IReadOnlyList<string> Objections,
    IReadOnlyList<string> AvoidTopics,
    int BacklogWindowDays = 14);
