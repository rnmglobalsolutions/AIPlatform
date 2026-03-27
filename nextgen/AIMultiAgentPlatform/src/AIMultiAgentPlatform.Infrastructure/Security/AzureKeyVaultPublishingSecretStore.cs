using System.Text;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace AIMultiAgentPlatform.Infrastructure.Security;

public sealed class AzureKeyVaultPublishingSecretStore : IPublishingSecretStore
{
    private readonly KeyVaultOptions _options;
    private readonly SecretClient _secretClient;

    public AzureKeyVaultPublishingSecretStore(KeyVaultOptions options, SecretClient secretClient)
    {
        _options = options;
        _secretClient = secretClient;
    }

    public async Task SaveAccessTokenAsync(PublishingAccessTokenSecret secret, CancellationToken cancellationToken)
    {
        var secretName = BuildSecretName(secret.SecretReference);
        var keyVaultSecret = new KeyVaultSecret(secretName, secret.AccessToken);
        keyVaultSecret.Properties.Tags["tenantId"] = secret.TenantId;
        keyVaultSecret.Properties.Tags["providerName"] = secret.ProviderName;
        keyVaultSecret.Properties.Tags["platform"] = secret.Platform;
        keyVaultSecret.Properties.Tags["secretReference"] = secret.SecretReference;

        await _secretClient.SetSecretAsync(keyVaultSecret, cancellationToken);
    }

    public async Task<string?> GetAccessTokenAsync(string secretReference, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _secretClient.GetSecretAsync(BuildSecretName(secretReference), cancellationToken: cancellationToken);
            return response.Value.Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public string BuildSecretName(string secretReference)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.SecretNamePrefix) ? "aimap-publishing" : _options.SecretNamePrefix.Trim().ToLowerInvariant();
        var normalizedReference = NormalizeSecretNameComponent(secretReference);
        return $"{prefix}-{normalizedReference}".Trim('-');
    }

    public static string NormalizeSecretNameComponent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "secret";
        }

        var builder = new StringBuilder(value.Length);
        var previousWasDash = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            var isAllowed = (character >= 'a' && character <= 'z') ||
                            (character >= '0' && character <= '9') ||
                            character == '-';
            var normalized = isAllowed ? character : '-';
            if (normalized == '-')
            {
                if (previousWasDash)
                {
                    continue;
                }

                previousWasDash = true;
                builder.Append(normalized);
                continue;
            }

            previousWasDash = false;
            builder.Append(normalized);
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "secret" : result;
    }

    public static SecretClient CreateSecretClient(KeyVaultOptions options)
    {
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(options.ManagedIdentityClientId))
        {
            credentialOptions.ManagedIdentityClientId = options.ManagedIdentityClientId;
        }

        return new SecretClient(new Uri(options.VaultUri), new DefaultAzureCredential(credentialOptions));
    }
}
