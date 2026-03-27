using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record ServiceBusOptions(
    bool Enabled,
    string ConnectionString,
    string CommandEntityPrefix,
    string EventEntityPrefix,
    string ProcessTallySubmissionEntityName,
    string GenerateDailyContentPackageEntityName,
    string ReviewAndScheduleDailyContentEntityName,
    string PublishScheduledContentEntityName)
{
    public bool HasRequiredConfiguration => !string.IsNullOrWhiteSpace(ConnectionString);

    public string ResolveCommandEntityName(string commandName) =>
        commandName switch
        {
            "process-tally-submission" => ProcessTallySubmissionEntityName,
            "generate-daily-content-package" => GenerateDailyContentPackageEntityName,
            "review-and-schedule-daily-content" => ReviewAndScheduleDailyContentEntityName,
            "publish-scheduled-content" => PublishScheduledContentEntityName,
            _ => BuildEntityName(CommandEntityPrefix, commandName)
        };

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
            configuration["ServiceBus:EventEntityPrefix"]?.Trim() ?? Default.EventEntityPrefix,
            configuration["ServiceBus:Commands:ProcessTallySubmissionEntityName"]?.Trim() ?? BuildEntityName(
                configuration["ServiceBus:CommandEntityPrefix"]?.Trim() ?? Default.CommandEntityPrefix,
                "process-tally-submission"),
            configuration["ServiceBus:Commands:GenerateDailyContentPackageEntityName"]?.Trim() ?? BuildEntityName(
                configuration["ServiceBus:CommandEntityPrefix"]?.Trim() ?? Default.CommandEntityPrefix,
                "generate-daily-content-package"),
            configuration["ServiceBus:Commands:ReviewAndScheduleDailyContentEntityName"]?.Trim() ?? BuildEntityName(
                configuration["ServiceBus:CommandEntityPrefix"]?.Trim() ?? Default.CommandEntityPrefix,
                "review-and-schedule-daily-content"),
            configuration["ServiceBus:Commands:PublishScheduledContentEntityName"]?.Trim() ?? BuildEntityName(
                configuration["ServiceBus:CommandEntityPrefix"]?.Trim() ?? Default.CommandEntityPrefix,
                "publish-scheduled-content"));
    }

    public static ServiceBusOptions Default => new(
        false,
        string.Empty,
        "cmd",
        "evt",
        "cmd-process-tally-submission",
        "cmd-generate-daily-content-package",
        "cmd-review-and-schedule-daily-content",
        "cmd-publish-scheduled-content");

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;

    private static string BuildEntityName(string prefix, string name) =>
        $"{prefix}-{NormalizeName(name)}";

    private static string NormalizeName(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        return builder.ToString().Trim('-');
    }
}
