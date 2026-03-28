using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Observability;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class PlatformOperationalReadinessServiceTests
{
    [Fact]
    public void GetReport_WhenProductionDependenciesAreConfigured_ReturnsHealthy()
    {
        var service = new PlatformOperationalReadinessService(
            new InfrastructureModeSettings(PlatformMode.Production, PersistenceMode.Sql, MessagingMode.ServiceBus, HostingMode.Dedicated),
            new ProductionInfrastructureOptions(SqlSkeletonEnabled: true, ServiceBusSkeletonEnabled: true, CommandOutboxEnabled: true),
            new SqlOptions(true, "Server=tcp:test.database.windows.net;Database=aimap;", "dbo", 30),
            new ServiceBusOptions(
                true,
                "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test=",
                "cmd",
                "evt",
                "cmd-process-tally-submission",
                "cmd-generate-daily-content-package",
                "cmd-review-and-schedule-daily-content",
                "cmd-publish-scheduled-content"),
            new KeyVaultOptions(true, "https://test-kv.vault.azure.net/", "aimap", "mi-client-id"),
            new BlobStorageOptions(true, "UseDevelopmentStorage=true", "videos"),
            new OpenAiOptions(true, "https://test.openai.azure.com/", "api-key", "gpt-5-mini", "gpt-5-mini", 4000, 6000),
            new HeyGenOptions(true, "heygen-key", "https://api.heygen.com/", "webhook-secret", "avatar-id", 30),
            new BufferOptions(true, "https://api.bufferapp.com/1/"),
            new MetricoolOptions(false, "https://app.metricool.com/api/", string.Empty, string.Empty, "Authorization", "Bearer", string.Empty),
            new FeatureFlagOptions(true, true, true, true, false, true, true),
            TimeProvider.System);

        var report = service.GetReport();

        Assert.True(report.IsReady);
        Assert.Equal("Healthy", report.OverallStatus);
        Assert.DoesNotContain(report.Components, component => component.Required && !component.Ready);
    }

    [Fact]
    public void GetReport_WhenProductionDependenciesAreMissing_ReturnsUnhealthy()
    {
        var service = new PlatformOperationalReadinessService(
            new InfrastructureModeSettings(PlatformMode.Production, PersistenceMode.Sql, MessagingMode.ServiceBus, HostingMode.Dedicated),
            ProductionInfrastructureOptions.Disabled,
            SqlOptions.Default,
            ServiceBusOptions.Default,
            KeyVaultOptions.Default,
            BlobStorageOptions.Default,
            OpenAiOptions.Default,
            HeyGenOptions.Default,
            BufferOptions.Default,
            MetricoolOptions.Default,
            new FeatureFlagOptions(true, true, true, true, false, true, true),
            TimeProvider.System);

        var report = service.GetReport();

        Assert.False(report.IsReady);
        Assert.Equal("Unhealthy", report.OverallStatus);
        Assert.Contains(report.Components, component => component.Name == "Persistence" && !component.Ready);
        Assert.Contains(report.Components, component => component.Name == "Messaging" && !component.Ready);
        Assert.Contains(report.Components, component => component.Name == "Secrets" && !component.Ready);
    }
}
