using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record OutboxOptions(
    int DispatchBatchSize,
    string DispatchSchedule)
{
    public static OutboxOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new OutboxOptions(
            ParseInt(configuration["Outbox:DispatchBatchSize"], Default.DispatchBatchSize),
            string.IsNullOrWhiteSpace(configuration["Outbox:DispatchSchedule"])
                ? Default.DispatchSchedule
                : configuration["Outbox:DispatchSchedule"]!.Trim());
    }

    public static OutboxOptions Default => new(25, "0 */1 * * * *");

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
