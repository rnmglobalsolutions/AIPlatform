using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record FeatureFlagOptions(
    bool EnableLlmStrategyPlanning,
    bool EnableLlmContentGeneration,
    bool EnableVideoGeneration,
    bool EnableSocialPublishing,
    bool EnableVoiceLeadAgent,
    bool EnableReportAgent,
    bool AllowHeuristicFallback)
{
    public static FeatureFlagOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new FeatureFlagOptions(
            ParseBool(configuration["FeatureFlags:EnableLlmStrategyPlanning"]),
            ParseBool(configuration["FeatureFlags:EnableLlmContentGeneration"]),
            ParseBool(configuration["FeatureFlags:EnableVideoGeneration"]),
            ParseBool(configuration["FeatureFlags:EnableSocialPublishing"]),
            ParseBool(configuration["FeatureFlags:EnableVoiceLeadAgent"]),
            ParseBool(configuration["FeatureFlags:EnableReportAgent"]),
            !bool.TryParse(configuration["FeatureFlags:AllowHeuristicFallback"], out var parsed) || parsed);
    }

    public static FeatureFlagOptions Default => new(
        false,
        false,
        false,
        false,
        false,
        false,
        true);

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
