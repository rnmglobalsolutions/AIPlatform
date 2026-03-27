using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record MetricoolOptions(
    bool Enabled,
    string BaseUrl,
    string PublishPathTemplate,
    string ReconcilePathTemplate,
    string AccessTokenHeaderName,
    string AccessTokenHeaderPrefix,
    string AccessTokenQueryParameterName)
{
    public bool HasRequiredConfiguration =>
        Enabled &&
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(PublishPathTemplate) &&
        !string.IsNullOrWhiteSpace(ReconcilePathTemplate) &&
        (!string.IsNullOrWhiteSpace(AccessTokenHeaderName) || !string.IsNullOrWhiteSpace(AccessTokenQueryParameterName));

    public static MetricoolOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new MetricoolOptions(
            ParseBool(configuration["Metricool:Enabled"]),
            configuration["Metricool:BaseUrl"]?.Trim() ?? Default.BaseUrl,
            configuration["Metricool:PublishPathTemplate"]?.Trim() ?? Default.PublishPathTemplate,
            configuration["Metricool:ReconcilePathTemplate"]?.Trim() ?? Default.ReconcilePathTemplate,
            configuration["Metricool:AccessTokenHeaderName"]?.Trim() ?? Default.AccessTokenHeaderName,
            configuration["Metricool:AccessTokenHeaderPrefix"]?.Trim() ?? Default.AccessTokenHeaderPrefix,
            configuration["Metricool:AccessTokenQueryParameterName"]?.Trim() ?? Default.AccessTokenQueryParameterName);
    }

    public static MetricoolOptions Default => new(
        false,
        "https://app.metricool.com/api/",
        string.Empty,
        string.Empty,
        "Authorization",
        "Bearer",
        string.Empty);

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
