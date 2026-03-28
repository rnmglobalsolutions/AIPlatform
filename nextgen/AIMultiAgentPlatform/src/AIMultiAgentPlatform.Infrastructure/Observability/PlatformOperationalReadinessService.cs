using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Observability;

public sealed class PlatformOperationalReadinessService(
    InfrastructureModeSettings settings,
    ProductionInfrastructureOptions productionOptions,
    SqlOptions sqlOptions,
    ServiceBusOptions serviceBusOptions,
    KeyVaultOptions keyVaultOptions,
    BlobStorageOptions blobStorageOptions,
    OpenAiOptions openAiOptions,
    HeyGenOptions heyGenOptions,
    BufferOptions bufferOptions,
    MetricoolOptions metricoolOptions,
    FeatureFlagOptions featureFlags,
    TimeProvider timeProvider)
{
    public PlatformReadinessReport GetReport()
    {
        var components = new List<PlatformComponentHealth>
        {
            BuildPersistenceHealth(settings, productionOptions, sqlOptions),
            BuildMessagingHealth(settings, productionOptions, serviceBusOptions),
            BuildKeyVaultHealth(settings, keyVaultOptions, featureFlags),
            BuildOpenAiHealth(featureFlags, openAiOptions),
            BuildVideoHealth(featureFlags, heyGenOptions, blobStorageOptions),
            BuildPublishingHealth(featureFlags, bufferOptions, metricoolOptions, keyVaultOptions, settings)
        };

        var overallStatus = components.Any(component => component.Required && !component.Ready)
            ? "Unhealthy"
            : components.Any(component => !component.Ready && !string.Equals(component.Status, "Healthy", StringComparison.Ordinal))
                ? "Degraded"
                : "Healthy";

        return new PlatformReadinessReport(
            settings.PlatformMode.ToString(),
            settings.PersistenceMode.ToString(),
            settings.MessagingMode.ToString(),
            settings.HostingMode.ToString(),
            overallStatus,
            timeProvider.GetUtcNow(),
            components);
    }

    private static PlatformComponentHealth BuildPersistenceHealth(
        InfrastructureModeSettings settings,
        ProductionInfrastructureOptions productionOptions,
        SqlOptions sqlOptions)
    {
        var sqlRequired = settings.PlatformMode == PlatformMode.Production || settings.PersistenceMode == PersistenceMode.Sql;
        var ready = !sqlRequired || (sqlOptions.Enabled && sqlOptions.HasRequiredConfiguration && productionOptions.SqlSkeletonEnabled);
        var detail = sqlRequired
            ? ready
                ? "SQL persistence is configured for production workloads."
                : "SQL persistence is required but missing connection string or production wiring."
            : settings.PersistenceMode == PersistenceMode.Table
                ? "Lean mode is using Azure Table Storage."
                : "Lean mode is using in-memory persistence.";

        return new PlatformComponentHealth(
            "Persistence",
            ready ? "Healthy" : "Unhealthy",
            sqlRequired,
            ready,
            detail);
    }

    private static PlatformComponentHealth BuildMessagingHealth(
        InfrastructureModeSettings settings,
        ProductionInfrastructureOptions productionOptions,
        ServiceBusOptions serviceBusOptions)
    {
        var serviceBusRequired = settings.PlatformMode == PlatformMode.Production || settings.MessagingMode == MessagingMode.ServiceBus;
        var ready = !serviceBusRequired || (
            serviceBusOptions.Enabled &&
            serviceBusOptions.HasRequiredConfiguration &&
            productionOptions.ServiceBusSkeletonEnabled &&
            productionOptions.CommandOutboxEnabled);

        var detail = serviceBusRequired
            ? ready
                ? "Service Bus and outbox dispatch are configured."
                : "Service Bus is required but connection string or outbox wiring is incomplete."
            : "Lean mode is running without durable messaging.";

        return new PlatformComponentHealth(
            "Messaging",
            ready ? "Healthy" : "Unhealthy",
            serviceBusRequired,
            ready,
            detail);
    }

    private static PlatformComponentHealth BuildKeyVaultHealth(
        InfrastructureModeSettings settings,
        KeyVaultOptions keyVaultOptions,
        FeatureFlagOptions featureFlags)
    {
        var required = settings.PlatformMode == PlatformMode.Production || featureFlags.EnableSocialPublishing;
        var ready = !required || keyVaultOptions.HasRequiredConfiguration;
        var detail = required
            ? ready
                ? "Key Vault is configured for runtime secret resolution."
                : "Key Vault is required for production-grade secret handling but is not fully configured."
            : "Key Vault is optional in lean mode when publishing is disabled.";

        return new PlatformComponentHealth(
            "Secrets",
            ready ? "Healthy" : "Unhealthy",
            required,
            ready,
            detail);
    }

    private static PlatformComponentHealth BuildOpenAiHealth(
        FeatureFlagOptions featureFlags,
        OpenAiOptions openAiOptions)
    {
        var required = featureFlags.EnableLlmStrategyPlanning ||
                       featureFlags.EnableLlmContentGeneration ||
                       featureFlags.EnableReportAgent;
        var ready = !required || (openAiOptions.Enabled && openAiOptions.HasRequiredConfiguration);
        var detail = required
            ? ready
                ? "OpenAI is configured for strategy, content, or reporting workloads."
                : "At least one LLM feature flag is enabled but OpenAI settings are incomplete."
            : "LLM features are disabled.";

        return new PlatformComponentHealth(
            "OpenAI",
            ready ? "Healthy" : "Unhealthy",
            required,
            ready,
            detail);
    }

    private static PlatformComponentHealth BuildVideoHealth(
        FeatureFlagOptions featureFlags,
        HeyGenOptions heyGenOptions,
        BlobStorageOptions blobStorageOptions)
    {
        var required = featureFlags.EnableVideoGeneration;
        var ready = !required || (
            heyGenOptions.Enabled &&
            heyGenOptions.HasRequiredConfiguration &&
            blobStorageOptions.Enabled &&
            blobStorageOptions.HasRequiredConfiguration);

        var detail = required
            ? ready
                ? "HeyGen and Blob Storage are configured for video generation."
                : "Video generation is enabled but HeyGen or Blob Storage configuration is incomplete."
            : "Video generation is disabled.";

        return new PlatformComponentHealth(
            "Video",
            ready ? "Healthy" : "Unhealthy",
            required,
            ready,
            detail);
    }

    private static PlatformComponentHealth BuildPublishingHealth(
        FeatureFlagOptions featureFlags,
        BufferOptions bufferOptions,
        MetricoolOptions metricoolOptions,
        KeyVaultOptions keyVaultOptions,
        InfrastructureModeSettings settings)
    {
        var required = featureFlags.EnableSocialPublishing;
        var providerCount = 0;

        if (bufferOptions.Enabled)
        {
            providerCount++;
        }

        if (metricoolOptions.Enabled && metricoolOptions.HasRequiredConfiguration)
        {
            providerCount++;
        }

        var providersReady = providerCount > 0;
        var secretHandlingReady = settings.PlatformMode != PlatformMode.Production || keyVaultOptions.HasRequiredConfiguration;
        var ready = !required || (providersReady && secretHandlingReady);

        var detail = !required
            ? "Social publishing is disabled."
            : ready
                ? $"Publishing is configured with {providerCount} provider(s) and production-grade secret handling."
                : "Social publishing is enabled but no real provider is ready or secret handling is incomplete.";

        return new PlatformComponentHealth(
            "Publishing",
            ready ? "Healthy" : "Unhealthy",
            required,
            ready,
            detail);
    }
}
