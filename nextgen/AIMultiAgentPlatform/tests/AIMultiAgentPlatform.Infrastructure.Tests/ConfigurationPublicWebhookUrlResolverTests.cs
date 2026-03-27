using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Video;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class ConfigurationPublicWebhookUrlResolverTests
{
    [Fact]
    public void ResolveHeyGenWebhookUrl_UsesFunctionsBaseUrlWhenConfigured()
    {
        var resolver = new ConfigurationPublicWebhookUrlResolver(
            new PublicEndpointOptions(
                string.Empty,
                "https://aimap-func.azurewebsites.net",
                string.Empty,
                "api/integrations/heygen/webhook"),
            new InfrastructureModeSettings(
                PlatformMode.Lean,
                PersistenceMode.InMemory,
                MessagingMode.Queue,
                HostingMode.CurrentRuntime));

        var resolved = resolver.ResolveHeyGenWebhookUrl();

        Assert.Equal("https://aimap-func.azurewebsites.net/api/integrations/heygen/webhook", resolved);
    }

    [Fact]
    public void ResolveHeyGenWebhookUrl_UsesExplicitWebhookUrlWhenPresent()
    {
        var resolver = new ConfigurationPublicWebhookUrlResolver(
            new PublicEndpointOptions(
                "https://public.test/custom/heygen/webhook",
                "https://ignored.test",
                string.Empty,
                "api/integrations/heygen/webhook"),
            new InfrastructureModeSettings(
                PlatformMode.Lean,
                PersistenceMode.InMemory,
                MessagingMode.Queue,
                HostingMode.FunctionsConsumption));

        var resolved = resolver.ResolveHeyGenWebhookUrl();

        Assert.Equal("https://public.test/custom/heygen/webhook", resolved);
    }
}
