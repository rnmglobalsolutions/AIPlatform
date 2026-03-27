using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record PublicEndpointOptions(
    string HeyGenWebhookUrl,
    string FunctionsBaseUrl,
    string ApiBaseUrl,
    string HeyGenWebhookPath)
{
    public static PublicEndpointOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new PublicEndpointOptions(
            configuration["PublicEndpoints:HeyGenWebhookUrl"]?.Trim() ?? string.Empty,
            configuration["PublicEndpoints:FunctionsBaseUrl"]?.Trim() ?? string.Empty,
            configuration["PublicEndpoints:ApiBaseUrl"]?.Trim() ?? string.Empty,
            configuration["PublicEndpoints:HeyGenWebhookPath"]?.Trim() ?? Default.HeyGenWebhookPath);
    }

    public static PublicEndpointOptions Default => new(
        string.Empty,
        string.Empty,
        string.Empty,
        "api/integrations/heygen/webhook");
}
