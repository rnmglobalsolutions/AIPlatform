using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record HeyGenOptions(
    bool Enabled,
    string ApiKey,
    string BaseUrl,
    string WebhookSecret,
    string DefaultAvatarId,
    int DefaultDurationSeconds)
{
    public bool HasRequiredConfiguration => !string.IsNullOrWhiteSpace(ApiKey);

    public static HeyGenOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new HeyGenOptions(
            ParseBool(configuration["HeyGen:Enabled"]),
            configuration["HeyGen:ApiKey"]?.Trim() ?? string.Empty,
            configuration["HeyGen:BaseUrl"]?.Trim() ?? Default.BaseUrl,
            configuration["HeyGen:WebhookSecret"]?.Trim() ?? string.Empty,
            configuration["HeyGen:DefaultAvatarId"]?.Trim() ?? string.Empty,
            ParseInt(configuration["HeyGen:DefaultDurationSeconds"], Default.DefaultDurationSeconds));
    }

    public static HeyGenOptions Default => new(
        false,
        string.Empty,
        "https://api.heygen.com/",
        string.Empty,
        string.Empty,
        30);

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
