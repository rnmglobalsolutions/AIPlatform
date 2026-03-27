using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record BufferOptions(
    bool Enabled,
    string BaseUrl)
{
    public static BufferOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new BufferOptions(
            ParseBool(configuration["Buffer:Enabled"]),
            configuration["Buffer:BaseUrl"]?.Trim() ?? Default.BaseUrl);
    }

    public static BufferOptions Default => new(false, "https://api.bufferapp.com/1/");

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
