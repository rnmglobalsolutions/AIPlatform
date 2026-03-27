using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record BlobStorageOptions(
    bool Enabled,
    string ConnectionString,
    string ContainerName)
{
    public bool HasRequiredConfiguration => !string.IsNullOrWhiteSpace(ConnectionString);

    public static BlobStorageOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        var connectionString =
            configuration["Storage:BlobConnectionString"] ??
            configuration["Storage__BlobConnectionString"] ??
            configuration["AzureWebJobsStorage"] ??
            string.Empty;

        return new BlobStorageOptions(
            ParseBool(configuration["Storage:EnableBlobVideoAssetStorage"]),
            connectionString.Trim(),
            configuration["Storage:GeneratedVideosContainerName"]?.Trim() ?? Default.ContainerName);
    }

    public static BlobStorageOptions Default => new(
        false,
        string.Empty,
        "aimap-generated-videos");

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
