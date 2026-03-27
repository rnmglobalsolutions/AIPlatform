using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Security;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class AzureKeyVaultPublishingSecretStoreTests
{
    [Theory]
    [InlineData("publish_secret_001", "aimap-publishing-publish-secret-001")]
    [InlineData("Publish Secret 001", "aimap-publishing-publish-secret-001")]
    [InlineData("metricool__instagram", "aimap-publishing-metricool-instagram")]
    public void NormalizeSecretReference_ProducesKeyVaultCompatibleName(string secretReference, string expectedSecretName)
    {
        var store = new AzureKeyVaultPublishingSecretStore(
            new KeyVaultOptions(true, "https://vault.test.vault.azure.net/", "aimap-publishing", string.Empty),
            AzureKeyVaultPublishingSecretStore.CreateSecretClient(
                new KeyVaultOptions(true, "https://vault.test.vault.azure.net/", "aimap-publishing", string.Empty)));

        var secretName = store.BuildSecretName(secretReference);

        Assert.Equal(expectedSecretName, secretName);
    }
}
