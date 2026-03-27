using AIMultiAgentPlatform.Domain.Strategy;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Strategy;

public sealed class BuildStrategicProfileUseCase
{
    public StrategicProfile Execute(ClientProfile profile)
    {
        var normalizedProfile = profile.Normalize();
        var conversionMode = ResolveConversionMode(normalizedProfile);
        return new StrategicProfile(
            CleanStrategicValue(normalizedProfile.BusinessName),
            CleanStrategicValue(normalizedProfile.Niche),
            CleanStrategicValue(normalizedProfile.Offer),
            CleanStrategicValue(normalizedProfile.TargetAudience),
            CleanStrategicValue(normalizedProfile.BrandTone),
            normalizedProfile.ContentLanguage,
            normalizedProfile.MainGoal.Trim(),
            normalizedProfile.DesiredAction.Trim(),
            normalizedProfile.CallToActionKeyword.Trim(),
            normalizedProfile.CalendlyUrl.Trim(),
            normalizedProfile.WebsiteUrl.Trim(),
            ResolvePrimaryOutcome(normalizedProfile),
            ResolveConversionDestination(normalizedProfile, conversionMode),
            ResolveLeadGoal(conversionMode),
            conversionMode,
            normalizedProfile.Platforms,
            normalizedProfile.PainPoints,
            normalizedProfile.Objections,
            normalizedProfile.AvoidTopics,
            ResolveContentPlanTier(normalizedProfile.ContentPlanTier));
    }

    private static string ResolvePrimaryOutcome(ClientProfile profile) =>
        FirstNonEmpty(profile.MainGoal, profile.DesiredAction, $"better results in {profile.Niche}");

    private static string ResolveConversionDestination(ClientProfile profile, StrategyConversionMode conversionMode)
    {
        return conversionMode switch
        {
            StrategyConversionMode.Booking => !string.IsNullOrWhiteSpace(profile.CalendlyUrl)
                ? "booking a consultation through the scheduling link"
                : "booking a consultation",
            StrategyConversionMode.Website => !string.IsNullOrWhiteSpace(profile.WebsiteUrl)
                ? "visiting the website and taking the next step there"
                : "visiting the website",
            StrategyConversionMode.DirectMessage => "starting a direct-message conversation",
            StrategyConversionMode.CommentKeyword => $"commenting with the keyword {profile.CallToActionKeyword}",
            _ => NormalizeTopicFragment(ResolvePrimaryOutcome(profile))
        };
    }

    private static string ResolveLeadGoal(StrategyConversionMode conversionMode) =>
        conversionMode switch
        {
            StrategyConversionMode.Booking => "book_consultation",
            StrategyConversionMode.Website => "visit_website",
            StrategyConversionMode.DirectMessage => "send_dm",
            StrategyConversionMode.CommentKeyword => "comment_keyword",
            _ => "generate_lead"
        };

    private static StrategyConversionMode ResolveConversionMode(ClientProfile profile)
    {
        var desiredAction = profile.DesiredAction;

        if (ContainsAny(desiredAction, "comment"))
        {
            return StrategyConversionMode.CommentKeyword;
        }

        if (ContainsAny(desiredAction, "dm", "message"))
        {
            return StrategyConversionMode.DirectMessage;
        }

        if (ContainsAny(desiredAction, "website", "site", "web", "page", "landing", "visit"))
        {
            return StrategyConversionMode.Website;
        }

        if (ContainsAny(desiredAction, "book", "consult", "call", "appointment"))
        {
            return StrategyConversionMode.Booking;
        }

        if (!string.IsNullOrWhiteSpace(profile.CalendlyUrl))
        {
            return StrategyConversionMode.Booking;
        }

        if (!string.IsNullOrWhiteSpace(profile.WebsiteUrl))
        {
            return StrategyConversionMode.Website;
        }

        return StrategyConversionMode.Generic;
    }

    private static bool ContainsAny(string? value, params string[] fragments)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeTopicFragment(string value) =>
        value.Trim().TrimEnd('.', '!', '?').ToLowerInvariant();

    private static string CleanStrategicValue(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value.Trim().TrimEnd('.', '!', '?'), @"\s+", " ");

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static ContentPlanTier ResolveContentPlanTier(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "growth" => ContentPlanTier.Growth,
            "premium" => ContentPlanTier.Premium,
            _ => ContentPlanTier.Starter
        };
}
