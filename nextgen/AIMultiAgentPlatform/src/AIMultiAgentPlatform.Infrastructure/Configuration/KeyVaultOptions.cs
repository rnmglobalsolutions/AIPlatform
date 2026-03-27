using Microsoft.Extensions.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Configuration;

public sealed record KeyVaultOptions(
    bool Enabled,
    string VaultUri,
    string SecretNamePrefix,
    string ManagedIdentityClientId)
{
    public bool HasRequiredConfiguration =>
        Enabled &&
        !string.IsNullOrWhiteSpace(VaultUri);

    public static KeyVaultOptions Resolve(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return Default;
        }

        return new KeyVaultOptions(
            ParseBool(configuration["KeyVault:Enabled"]),
            configuration["KeyVault:VaultUri"]?.Trim() ?? Default.VaultUri,
            configuration["KeyVault:SecretNamePrefix"]?.Trim() ?? Default.SecretNamePrefix,
            configuration["KeyVault:ManagedIdentityClientId"]?.Trim() ?? Default.ManagedIdentityClientId);
    }

    public static KeyVaultOptions Default => new(
        false,
        string.Empty,
        "aimap-publishing",
        string.Empty);

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
