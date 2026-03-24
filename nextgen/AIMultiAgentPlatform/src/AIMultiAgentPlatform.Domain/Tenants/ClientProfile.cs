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
    IReadOnlyList<string> AvoidTopics)
{
    public ClientProfile Normalize() =>
        this with
        {
            BusinessName = NormalizeValue(BusinessName, "Unnamed Business"),
            PrimaryContactName = NormalizeValue(PrimaryContactName, "Unknown Contact"),
            PrimaryContactEmail = NormalizeValue(PrimaryContactEmail, "unknown@example.invalid").ToLowerInvariant(),
            Niche = NormalizeValue(Niche, "General Business"),
            Offer = NormalizeValue(Offer, "Growth services"),
            TargetAudience = NormalizeValue(TargetAudience, "Business owners"),
            BrandTone = NormalizeValue(BrandTone, "Professional"),
            CallToActionKeyword = NormalizeValue(CallToActionKeyword, "BOOK"),
            Platforms = NormalizeList(Platforms, ["Instagram"]),
            PainPoints = NormalizeList(PainPoints, ["Low visibility", "Inconsistent lead flow"]),
            Objections = NormalizeList(Objections, ["No time", "Unsure what to post"]),
            AvoidTopics = NormalizeList(AvoidTopics, Array.Empty<string>())
        };

    private static string NormalizeValue(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

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
