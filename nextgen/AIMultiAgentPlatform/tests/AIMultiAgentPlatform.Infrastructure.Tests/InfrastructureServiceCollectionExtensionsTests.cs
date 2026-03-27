using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.DependencyInjection;
using AIMultiAgentPlatform.Infrastructure.Messaging.InMemory;
using AIMultiAgentPlatform.Infrastructure.Messaging.ServiceBus;
using AIMultiAgentPlatform.Infrastructure.Persistence.Sql;
using AIMultiAgentPlatform.Infrastructure.Storage;
using AIMultiAgentPlatform.Infrastructure.AI.OpenAi;
using AIMultiAgentPlatform.Infrastructure.Video.HeyGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_WhenLeanTableModeIsRequestedWithoutConfiguration_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(
                platformMode: "Lean",
                persistenceMode: "Table",
                messagingMode: "Queue",
                hostingMode: "FunctionsConsumption"));

        Assert.Contains("Lean table persistence requires IConfiguration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInfrastructure_WhenProductionSqlModeIsRequested_RegistersProductionSkeleton()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(
            platformMode: "Production",
            persistenceMode: "Sql",
            messagingMode: "ServiceBus",
            hostingMode: "Dedicated");

        using var provider = services.BuildServiceProvider();
        var productionOptions = provider.GetRequiredService<ProductionInfrastructureOptions>();
        var commandBus = provider.GetRequiredService<ICommandBus>();
        var eventBus = provider.GetRequiredService<IEventBus>();

        Assert.True(productionOptions.SqlSkeletonEnabled);
        Assert.True(productionOptions.ServiceBusSkeletonEnabled);
        Assert.IsType<InMemoryCommandBus>(commandBus);
        Assert.IsType<InMemoryEventBus>(eventBus);
    }

    [Fact]
    public void AddInfrastructure_RegistersControlPlaneDefaults()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();

        var openAiOptions = provider.GetRequiredService<OpenAiOptions>();
        var featureFlags = provider.GetRequiredService<FeatureFlagOptions>();
        var publicEndpointResolver = provider.GetRequiredService<IPublicWebhookUrlResolver>();
        var sqlOptions = provider.GetRequiredService<SqlOptions>();
        var serviceBusOptions = provider.GetRequiredService<ServiceBusOptions>();
        var commandBus = provider.GetRequiredService<ICommandBus>();
        var eventBus = provider.GetRequiredService<IEventBus>();
        var strategyPlanner = provider.GetRequiredService<ILLMStrategyPlanner>();
        var contentGenerator = provider.GetRequiredService<ILLMContentGenerator>();
        var guardrailValidator = provider.GetRequiredService<IContentGuardrailValidator>();
        var contentMemoryRepository = provider.GetRequiredService<IContentMemoryRepository>();
        var webhookRegistrationRepository = provider.GetRequiredService<IVideoWebhookEndpointRegistrationRepository>();
        var videoJobRepository = provider.GetRequiredService<IVideoGenerationJobRepository>();
        var generatedVideoAssetRepository = provider.GetRequiredService<IGeneratedVideoAssetRepository>();
        var connectedPublishingProfileRepository = provider.GetRequiredService<IConnectedPublishingProfileRepository>();
        var publishedContentRecordRepository = provider.GetRequiredService<IPublishedContentRecordRepository>();
        var videoProvider = provider.GetRequiredService<IVideoGenerationProvider>();
        var videoAssetStore = provider.GetRequiredService<IVideoAssetStore>();
        var webhookEndpointManager = provider.GetRequiredService<IWebhookEndpointManager>();
        var publishingProvider = provider.GetRequiredService<IPublishingProvider>();

        Assert.False(openAiOptions.Enabled);
        Assert.False(sqlOptions.Enabled);
        Assert.False(serviceBusOptions.Enabled);
        Assert.False(featureFlags.EnableLlmStrategyPlanning);
        Assert.False(featureFlags.EnableLlmContentGeneration);
        Assert.True(featureFlags.AllowHeuristicFallback);
        Assert.NotNull(publicEndpointResolver);
        Assert.NotNull(commandBus);
        Assert.NotNull(eventBus);
        Assert.NotNull(strategyPlanner);
        Assert.NotNull(contentGenerator);
        Assert.NotNull(guardrailValidator);
        Assert.NotNull(contentMemoryRepository);
        Assert.NotNull(webhookRegistrationRepository);
        Assert.NotNull(videoJobRepository);
        Assert.NotNull(generatedVideoAssetRepository);
        Assert.NotNull(connectedPublishingProfileRepository);
        Assert.NotNull(publishedContentRecordRepository);
        Assert.NotNull(videoProvider);
        Assert.NotNull(videoAssetStore);
        Assert.NotNull(webhookEndpointManager);
        Assert.NotNull(publishingProvider);
    }

    [Fact]
    public async Task AddInfrastructure_WhenProductionSqlAndServiceBusAreConfigured_RegistersConcreteSkeletonServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlatformMode"] = "Production",
                ["Infrastructure:PersistenceMode"] = "Sql",
                ["Infrastructure:MessagingMode"] = "ServiceBus",
                ["Infrastructure:HostingMode"] = "Dedicated",
                ["Sql:Enabled"] = "true",
                ["Sql:ConnectionString"] = "Server=(localdb)\\mssqllocaldb;Database=Aimap;Trusted_Connection=True;",
                ["ServiceBus:Enabled"] = "true",
                ["ServiceBus:ConnectionString"] = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test="
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();
        var productionOptions = provider.GetRequiredService<ProductionInfrastructureOptions>();
        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AiPlatformDbContext>>();
        var commandBus = provider.GetRequiredService<ICommandBus>();
        var eventBus = provider.GetRequiredService<IEventBus>();

        Assert.True(productionOptions.SqlSkeletonEnabled);
        Assert.True(productionOptions.ServiceBusSkeletonEnabled);
        Assert.IsType<ServiceBusCommandBus>(commandBus);
        Assert.IsType<ServiceBusEventBus>(eventBus);
        Assert.NotNull(dbContextFactory);
    }

    [Fact]
    public void SqlOptions_Resolve_UsesFallbacks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sql:Enabled"] = "true",
                ["Sql:CommandTimeoutSeconds"] = "0"
            })
            .Build();

        var options = SqlOptions.Resolve(configuration);

        Assert.True(options.Enabled);
        Assert.Equal(SqlOptions.Default.CommandTimeoutSeconds, options.CommandTimeoutSeconds);
        Assert.False(options.HasRequiredConfiguration);
    }

    [Fact]
    public void ServiceBusOptions_Resolve_UsesFallbacks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceBus:Enabled"] = "true"
            })
            .Build();

        var options = ServiceBusOptions.Resolve(configuration);

        Assert.True(options.Enabled);
        Assert.Equal(ServiceBusOptions.Default.CommandEntityPrefix, options.CommandEntityPrefix);
        Assert.Equal(ServiceBusOptions.Default.EventEntityPrefix, options.EventEntityPrefix);
        Assert.False(options.HasRequiredConfiguration);
    }

    [Fact]
    public async Task AddInfrastructure_DefaultGuardrailValidator_RejectsInvalidJson()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();
        var guardrailValidator = provider.GetRequiredService<IContentGuardrailValidator>();

        var result = await guardrailValidator.ValidateAsync(
            new ContentGuardrailValidationRequest(
                "daily-content",
                "{ invalid json",
                BlockedTopics: ["politics"]),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("valid JSON", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddInfrastructure_DefaultGuardrailValidator_WarnsOnBlockedTopics()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();
        var guardrailValidator = provider.GetRequiredService<IContentGuardrailValidator>();

        var result = await guardrailValidator.ValidateAsync(
            new ContentGuardrailValidationRequest(
                "daily-content",
                """{"caption":"This post talks about politics and market trends."}""",
                BlockedTopics: ["politics"]),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, warning => warning.Contains("blocked topic 'politics'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AddInfrastructure_WhenLlmFeatureFlagIsEnabledWithoutRequiredConfiguration_FailsExplicitly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:Enabled"] = "true",
                ["FeatureFlags:EnableLlmStrategyPlanning"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var planner = provider.GetRequiredService<ILLMStrategyPlanner>();

        var result = await planner.GenerateAsync(
            new StrategyPlannerRequest(
                "tenant-1",
                "corr-1",
                "v1",
                string.Empty,
                "system",
                "user"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("configuration is incomplete", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddInfrastructure_WhenLlmStrategyPlanningIsConfigured_RegistersConcretePlanner()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:Enabled"] = "true",
                ["OpenAI:Endpoint"] = "https://example.openai.azure.com/",
                ["OpenAI:ApiKey"] = "test-key",
                ["FeatureFlags:EnableLlmStrategyPlanning"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var planner = provider.GetRequiredService<ILLMStrategyPlanner>();

        Assert.IsType<OpenAiStrategyPlanner>(planner);
    }

    [Fact]
    public void AddInfrastructure_WhenLlmContentGenerationIsConfigured_RegistersConcreteGenerator()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:Enabled"] = "true",
                ["OpenAI:Endpoint"] = "https://example.openai.azure.com/",
                ["OpenAI:ApiKey"] = "test-key",
                ["FeatureFlags:EnableLlmContentGeneration"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<ILLMContentGenerator>();

        Assert.IsType<OpenAiContentGenerator>(generator);
    }

    [Fact]
    public void AddInfrastructure_WhenVideoGenerationIsConfigured_RegistersConcreteHeyGenProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HeyGen:Enabled"] = "true",
                ["HeyGen:ApiKey"] = "test-key",
                ["FeatureFlags:EnableVideoGeneration"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var videoProvider = provider.GetRequiredService<IVideoGenerationProvider>();

        Assert.IsType<HeyGenVideoGenerationProvider>(videoProvider);
    }

    [Fact]
    public void AddInfrastructure_WhenBlobVideoStorageIsConfigured_RegistersConcreteStore()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:EnableBlobVideoAssetStorage"] = "true",
                ["Storage:BlobConnectionString"] = "UseDevelopmentStorage=true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var videoAssetStore = provider.GetRequiredService<IVideoAssetStore>();

        Assert.IsType<AzureBlobVideoAssetStore>(videoAssetStore);
    }

    [Fact]
    public void OpenAiOptions_Resolve_UsesFallbackForNonPositiveTokenLimits()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:Enabled"] = "true",
                ["OpenAI:StrategyMaxOutputTokens"] = "0",
                ["OpenAI:ContentMaxOutputTokens"] = "-20"
            })
            .Build();

        var options = OpenAiOptions.Resolve(configuration);

        Assert.Equal(OpenAiOptions.Default.StrategyMaxOutputTokens, options.StrategyMaxOutputTokens);
        Assert.Equal(OpenAiOptions.Default.ContentMaxOutputTokens, options.ContentMaxOutputTokens);
        Assert.False(options.HasRequiredConfiguration);
    }
}
