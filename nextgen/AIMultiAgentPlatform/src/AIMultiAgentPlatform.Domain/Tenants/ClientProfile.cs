namespace AIMultiAgentPlatform.Domain.Tenants;

public sealed record ClientProfile(
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
    string PrimaryContactPhone = "",
    IReadOnlyList<string>? PlatformLinks = null,
    string CalendlyUrl = "",
    string MainGoal = "",
    string DesiredAction = "",
    string ContentLanguage = "English")
{
    public ClientProfile Normalize() =>
        this with
        {
            BusinessName = NormalizeValue(BusinessName, "Unnamed Business"),
            PrimaryContactName = NormalizeValue(PrimaryContactName, "Unknown Contact"),
            PrimaryContactEmail = NormalizeValue(PrimaryContactEmail, "unknown@example.invalid").ToLowerInvariant(),
            PrimaryContactPhone = NormalizeOptionalValue(PrimaryContactPhone),
            Niche = NormalizeValue(Niche, "General Business"),
            Offer = NormalizeValue(Offer, "Growth services"),
            TargetAudience = NormalizeValue(TargetAudience, "Business owners"),
            BrandTone = NormalizeValue(BrandTone, "Professional"),
            CallToActionKeyword = NormalizeValue(CallToActionKeyword, "BOOK"),
            Platforms = NormalizeList(Platforms, ["Instagram"]),
            PlatformLinks = NormalizeList(PlatformLinks, Array.Empty<string>()),
            CalendlyUrl = NormalizeOptionalValue(CalendlyUrl),
            MainGoal = NormalizeValue(MainGoal, "Generate more leads"),
            DesiredAction = NormalizeValue(DesiredAction, "Comment or DM for more details"),
            ContentLanguage = NormalizeLanguage(ContentLanguage),
            PainPoints = NormalizeList(PainPoints, ["Low visibility", "Inconsistent lead flow"]),
            Objections = NormalizeList(Objections, ["No time", "Unsure what to post"]),
            AvoidTopics = NormalizeList(AvoidTopics, Array.Empty<string>())
        };

    private static string NormalizeValue(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeOptionalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeLanguage(string? value)
    {
        var normalized = NormalizeValue(value, "English");

        return normalized.ToLowerInvariant() switch
        {
            "english" => "English",
            "spanish" => "Spanish",
            "bilingual" => "Bilingual",
            _ => "English"
        };
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values, IReadOnlyList<string> fallback)
    {
        var cleaned = values?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned is { Length: > 0 } ? cleaned : fallback.ToArray();
    }
}
