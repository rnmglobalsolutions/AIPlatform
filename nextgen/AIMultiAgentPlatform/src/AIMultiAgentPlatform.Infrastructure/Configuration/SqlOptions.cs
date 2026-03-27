using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record SqlOptions(
    bool Enabled,
    string ConnectionString,
    string Schema,
    int CommandTimeoutSeconds)
{
    public bool HasRequiredConfiguration => !string.IsNullOrWhiteSpace(ConnectionString);

    public static SqlOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new SqlOptions(
            ParseBool(configuration["Sql:Enabled"]),
            configuration["Sql:ConnectionString"]?.Trim() ?? string.Empty,
            configuration["Sql:Schema"]?.Trim() ?? Default.Schema,
            ParseInt(configuration["Sql:CommandTimeoutSeconds"], Default.CommandTimeoutSeconds));
    }

    public static SqlOptions Default => new(
        false,
        string.Empty,
        "dbo",
        30);

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
