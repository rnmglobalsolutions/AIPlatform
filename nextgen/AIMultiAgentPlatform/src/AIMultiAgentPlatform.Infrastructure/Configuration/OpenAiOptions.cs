using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record OpenAiOptions(
    bool Enabled,
    string Endpoint,
    string ApiKey,
    string StrategyModel,
    string ContentModel,
    int StrategyMaxOutputTokens,
    int ContentMaxOutputTokens)
{
    public bool HasRequiredConfiguration =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(ApiKey);

    public static OpenAiOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new OpenAiOptions(
            ParseBool(configuration["OpenAI:Enabled"]),
            configuration["OpenAI:Endpoint"]?.Trim() ?? string.Empty,
            configuration["OpenAI:ApiKey"]?.Trim() ?? string.Empty,
            configuration["OpenAI:StrategyModel"]?.Trim() ?? Default.StrategyModel,
            configuration["OpenAI:ContentModel"]?.Trim() ?? Default.ContentModel,
            ParseInt(configuration["OpenAI:StrategyMaxOutputTokens"], Default.StrategyMaxOutputTokens),
            ParseInt(configuration["OpenAI:ContentMaxOutputTokens"], Default.ContentMaxOutputTokens));
    }

    public static OpenAiOptions Default => new(
        false,
        string.Empty,
        string.Empty,
        "gpt-5-mini",
        "gpt-5-mini",
        4000,
        6000);

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
