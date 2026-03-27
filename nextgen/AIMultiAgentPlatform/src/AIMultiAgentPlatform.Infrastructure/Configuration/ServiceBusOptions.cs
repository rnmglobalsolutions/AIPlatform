using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record ServiceBusOptions(
    bool Enabled,
    string ConnectionString,
    string CommandEntityPrefix,
    string EventEntityPrefix)
{
    public bool HasRequiredConfiguration => !string.IsNullOrWhiteSpace(ConnectionString);

    public static ServiceBusOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new ServiceBusOptions(
            ParseBool(configuration["ServiceBus:Enabled"]),
            configuration["ServiceBus:ConnectionString"]?.Trim() ?? string.Empty,
            configuration["ServiceBus:CommandEntityPrefix"]?.Trim() ?? Default.CommandEntityPrefix,
            configuration["ServiceBus:EventEntityPrefix"]?.Trim() ?? Default.EventEntityPrefix);
    }

    public static ServiceBusOptions Default => new(
        false,
        string.Empty,
        "cmd",
        "evt");

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
