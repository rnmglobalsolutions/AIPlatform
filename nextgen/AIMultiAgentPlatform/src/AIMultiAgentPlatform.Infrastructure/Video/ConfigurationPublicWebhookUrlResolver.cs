using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Video;

public sealed class ConfigurationPublicWebhookUrlResolver(
    PublicEndpointOptions options,
    InfrastructureModeSettings infrastructureSettings) : IPublicWebhookUrlResolver
{
    public string? ResolveHeyGenWebhookUrl()
    {
        if (!string.IsNullOrWhiteSpace(options.HeyGenWebhookUrl))
        {
            return NormalizeAbsoluteUrl(options.HeyGenWebhookUrl);
        }

        if (!string.IsNullOrWhiteSpace(options.FunctionsBaseUrl))
        {
            return Combine(options.FunctionsBaseUrl, options.HeyGenWebhookPath);
        }

        var websiteHostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")?.Trim();
        var isFunctionsRuntime =
            infrastructureSettings.HostingMode == HostingMode.FunctionsConsumption ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME"));

        if (isFunctionsRuntime && !string.IsNullOrWhiteSpace(websiteHostname))
        {
            return Combine($"https://{websiteHostname}", options.HeyGenWebhookPath);
        }

        return null;
    }

    private static string? NormalizeAbsoluteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            ? uri.ToString().TrimEnd('/')
            : null;
    }

    private static string? Combine(string? baseUrl, string? path)
    {
        var normalizedBaseUrl = NormalizeAbsoluteUrl(baseUrl);
        if (normalizedBaseUrl is null)
        {
            return null;
        }

        var normalizedPath = string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim().TrimStart('/');

        return string.IsNullOrWhiteSpace(normalizedPath)
            ? normalizedBaseUrl
            : $"{normalizedBaseUrl}/{normalizedPath}";
    }
}
