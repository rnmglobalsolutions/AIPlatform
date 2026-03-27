using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Abstractions.Voice;
using AIMultiAgentPlatform.Infrastructure.AI.InMemory;
using AIMultiAgentPlatform.Infrastructure.AI.OpenAi;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Persistence;
using AIMultiAgentPlatform.Infrastructure.Persistence.TableStorage;
using AIMultiAgentPlatform.Infrastructure.Persistence.Sql;
using AIMultiAgentPlatform.Infrastructure.Publishing.InMemory;
using AIMultiAgentPlatform.Infrastructure.Publishing.Buffer;
using AIMultiAgentPlatform.Infrastructure.Reporting;
using AIMultiAgentPlatform.Infrastructure.Storage;
using AIMultiAgentPlatform.Infrastructure.Messaging.InMemory;
using AIMultiAgentPlatform.Infrastructure.Messaging.ServiceBus;
using AIMultiAgentPlatform.Infrastructure.Video;
using AIMultiAgentPlatform.Infrastructure.Video.HeyGen;
using AIMultiAgentPlatform.Infrastructure.Video.InMemory;
using AIMultiAgentPlatform.Infrastructure.Voice;
using Azure.Data.Tables;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIMultiAgentPlatform.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = InfrastructureModeSettings.Resolve(
            configuration["PlatformMode"],
            configuration["Infrastructure:PersistenceMode"],
            configuration["Infrastructure:MessagingMode"],
            configuration["Infrastructure:HostingMode"]);

        services.AddSingleton(settings);
        services.AddSharedInfrastructureCore(configuration);

        return settings.PlatformMode switch
        {
            PlatformMode.Lean => services.AddLeanInfrastructure(settings, configuration),
            PlatformMode.Production => services.AddProductionInfrastructure(settings, configuration),
            _ => services.AddLeanInfrastructure(settings, configuration)
        };
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? platformMode = null,
        string? persistenceMode = null,
        string? messagingMode = null,
        string? hostingMode = null)
    {
        var settings = InfrastructureModeSettings.Resolve(platformMode, persistenceMode, messagingMode, hostingMode);
        services.AddSingleton(settings);

        services.AddSharedInfrastructureCore(configuration: null);

        return settings.PlatformMode switch
        {
            PlatformMode.Lean => services.AddLeanInfrastructure(settings, configuration: null),
            PlatformMode.Production => services.AddProductionInfrastructure(settings, configuration: null),
            _ => services.AddLeanInfrastructure(settings, configuration: null)
        };
    }

    private static IServiceCollection AddSharedInfrastructureCore(this IServiceCollection services, IConfiguration? configuration)
    {
        var openAiOptions = OpenAiOptions.Resolve(configuration);
        var heyGenOptions = HeyGenOptions.Resolve(configuration);
        var blobStorageOptions = BlobStorageOptions.Resolve(configuration);
        var bufferOptions = BufferOptions.Resolve(configuration);
        var publicEndpointOptions = PublicEndpointOptions.Resolve(configuration);
        var sqlOptions = SqlOptions.Resolve(configuration);
        var serviceBusOptions = ServiceBusOptions.Resolve(configuration);
        var featureFlags = FeatureFlagOptions.Resolve(configuration);

        services.AddSingleton(openAiOptions);
        services.AddSingleton(heyGenOptions);
        services.AddSingleton(blobStorageOptions);
        services.AddSingleton(bufferOptions);
        services.AddSingleton(publicEndpointOptions);
        services.AddSingleton(sqlOptions);
        services.AddSingleton(serviceBusOptions);
        services.AddSingleton(featureFlags);
        services.AddSingleton<IPublicWebhookUrlResolver, ConfigurationPublicWebhookUrlResolver>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();
        services.AddSingleton<ICommandBus, InMemoryCommandBus>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<ILLMStrategyPlanner, InMemoryLlmStrategyPlanner>();
        services.AddSingleton<ILLMContentGenerator, InMemoryLlmContentGenerator>();
        services.AddSingleton<IContentGuardrailValidator, PermissiveContentGuardrailValidator>();
        services.AddSingleton<IVideoGenerationProvider, InMemoryVideoGenerationProvider>();
        services.AddSingleton<IVideoAssetStore, InMemoryVideoAssetStore>();
        services.AddSingleton<IWebhookEndpointManager, InMemoryWebhookEndpointManager>();
        services.AddSingleton<IPublishingProvider, InMemoryPublishingProvider>();

        if (featureFlags.EnableSocialPublishing && bufferOptions.Enabled)
        {
            services.AddSingleton<BufferPublishingProvider>();
            services.AddSingleton<IPublishingProvider>(sp => sp.GetRequiredService<BufferPublishingProvider>());
        }

        if (featureFlags.EnableLlmStrategyPlanning &&
            openAiOptions.Enabled &&
            openAiOptions.HasRequiredConfiguration)
        {
            services.AddSingleton<OpenAiStrategyPlanner>();
            services.AddSingleton<ILLMStrategyPlanner>(sp => sp.GetRequiredService<OpenAiStrategyPlanner>());
        }

        if (featureFlags.EnableLlmContentGeneration &&
            openAiOptions.Enabled &&
            openAiOptions.HasRequiredConfiguration)
        {
            services.AddSingleton<OpenAiContentGenerator>();
            services.AddSingleton<ILLMContentGenerator>(sp => sp.GetRequiredService<OpenAiContentGenerator>());
        }

        if (featureFlags.EnableVideoGeneration &&
            heyGenOptions.Enabled &&
            heyGenOptions.HasRequiredConfiguration)
        {
            services.AddSingleton<HeyGenVideoGenerationProvider>();
            services.AddSingleton<IVideoGenerationProvider>(sp => sp.GetRequiredService<HeyGenVideoGenerationProvider>());
            services.AddSingleton<HeyGenWebhookEndpointManager>();
            services.AddSingleton<IWebhookEndpointManager>(sp => sp.GetRequiredService<HeyGenWebhookEndpointManager>());
        }

        if (blobStorageOptions.Enabled &&
            blobStorageOptions.HasRequiredConfiguration)
        {
            services.AddSingleton<AzureBlobVideoAssetStore>();
            services.AddSingleton<IVideoAssetStore>(sp => sp.GetRequiredService<AzureBlobVideoAssetStore>());
        }

        return services;
    }

    private static IServiceCollection AddLeanInfrastructure(
        this IServiceCollection services,
        InfrastructureModeSettings settings,
        IConfiguration? configuration)
    {
        if (settings.PersistenceMode == PersistenceMode.Table)
        {
            if (configuration is null)
            {
                throw new InvalidOperationException("Lean table persistence requires IConfiguration so storage settings can be resolved.");
            }

            services.AddTableStorageInfrastructureAdapters(configuration);
            return services.AddRemainingInMemoryInfrastructureAdapters(skipTenantSlice: true);
        }

        return services.AddInMemoryInfrastructureAdapters();
    }

    private static IServiceCollection AddProductionInfrastructure(
        this IServiceCollection services,
        InfrastructureModeSettings settings,
        IConfiguration? configuration)
    {
        var productionOptions = ProductionInfrastructureOptions.Disabled;

        if (settings.PersistenceMode == PersistenceMode.Sql)
        {
            services.AddSqlInfrastructureSkeleton(configuration);
            productionOptions = productionOptions with { SqlSkeletonEnabled = true };
        }

        if (settings.MessagingMode == MessagingMode.ServiceBus)
        {
            services.AddServiceBusInfrastructureSkeleton(configuration);
            productionOptions = productionOptions with { ServiceBusSkeletonEnabled = true };
        }

        services.AddSingleton(productionOptions);
        return services.AddInMemoryInfrastructureAdapters();
    }

    private static IServiceCollection AddSqlInfrastructureSkeleton(
        this IServiceCollection services,
        IConfiguration? configuration)
    {
        var sqlOptions = SqlOptions.Resolve(configuration);
        services.AddSingleton(sqlOptions);

        if (!sqlOptions.Enabled || !sqlOptions.HasRequiredConfiguration)
        {
            return services;
        }

        services.AddDbContextFactory<AiPlatformDbContext>(options =>
        {
            options.UseSqlServer(
                sqlOptions.ConnectionString,
                sqlServerOptions => sqlServerOptions.CommandTimeout(sqlOptions.CommandTimeoutSeconds));
        });

        return services;
    }

    private static IServiceCollection AddServiceBusInfrastructureSkeleton(
        this IServiceCollection services,
        IConfiguration? configuration)
    {
        var serviceBusOptions = ServiceBusOptions.Resolve(configuration);
        services.AddSingleton(serviceBusOptions);

        if (!serviceBusOptions.Enabled || !serviceBusOptions.HasRequiredConfiguration)
        {
            return services;
        }

        services.AddSingleton(new ServiceBusClient(serviceBusOptions.ConnectionString));
        services.AddSingleton<ServiceBusCommandBus>();
        services.AddSingleton<ICommandBus>(sp => sp.GetRequiredService<ServiceBusCommandBus>());
        services.AddSingleton<ServiceBusEventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<ServiceBusEventBus>());

        return services;
    }

    private static IServiceCollection AddInMemoryInfrastructureAdapters(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryTenantRepository>();
        services.AddSingleton<ITenantRepository>(sp => sp.GetRequiredService<InMemoryTenantRepository>());
        services.AddSingleton<InMemoryTallySubmissionReceiptRepository>();
        services.AddSingleton<ITallySubmissionReceiptRepository>(sp => sp.GetRequiredService<InMemoryTallySubmissionReceiptRepository>());
        services.AddSingleton<InMemoryStrategyPlanRepository>();
        services.AddSingleton<IStrategyPlanRepository>(sp => sp.GetRequiredService<InMemoryStrategyPlanRepository>());
        services.AddSingleton<InMemoryContentMemoryRepository>();
        services.AddSingleton<IContentMemoryRepository>(sp => sp.GetRequiredService<InMemoryContentMemoryRepository>());
        services.AddSingleton<InMemoryEditorialBacklogRepository>();
        services.AddSingleton<IEditorialBacklogRepository>(sp => sp.GetRequiredService<InMemoryEditorialBacklogRepository>());
        services.AddSingleton<InMemoryDailyContentRequestRepository>();
        services.AddSingleton<IDailyContentRequestRepository>(sp => sp.GetRequiredService<InMemoryDailyContentRequestRepository>());
        services.AddSingleton<InMemoryDailyContentBriefRepository>();
        services.AddSingleton<IDailyContentBriefRepository>(sp => sp.GetRequiredService<InMemoryDailyContentBriefRepository>());
        services.AddSingleton<InMemoryPrimaryAssetRepository>();
        services.AddSingleton<IPrimaryAssetRepository>(sp => sp.GetRequiredService<InMemoryPrimaryAssetRepository>());
        services.AddSingleton<InMemoryCaptionAssetRepository>();
        services.AddSingleton<ICaptionAssetRepository>(sp => sp.GetRequiredService<InMemoryCaptionAssetRepository>());
        services.AddSingleton<InMemoryRepurposedAssetBundleRepository>();
        services.AddSingleton<IRepurposedAssetBundleRepository>(sp => sp.GetRequiredService<InMemoryRepurposedAssetBundleRepository>());
        services.AddSingleton<InMemoryVideoGenerationJobRepository>();
        services.AddSingleton<IVideoGenerationJobRepository>(sp => sp.GetRequiredService<InMemoryVideoGenerationJobRepository>());
        services.AddSingleton<InMemoryGeneratedVideoAssetRepository>();
        services.AddSingleton<IGeneratedVideoAssetRepository>(sp => sp.GetRequiredService<InMemoryGeneratedVideoAssetRepository>());
        services.AddSingleton<InMemoryVideoWebhookEndpointRegistrationRepository>();
        services.AddSingleton<IVideoWebhookEndpointRegistrationRepository>(sp => sp.GetRequiredService<InMemoryVideoWebhookEndpointRegistrationRepository>());
        services.AddSingleton<InMemoryComplianceReviewRepository>();
        services.AddSingleton<IComplianceReviewRepository>(sp => sp.GetRequiredService<InMemoryComplianceReviewRepository>());
        services.AddSingleton<InMemoryQualityReviewRepository>();
        services.AddSingleton<IQualityReviewRepository>(sp => sp.GetRequiredService<InMemoryQualityReviewRepository>());
        services.AddSingleton<InMemoryApprovalRequestRepository>();
        services.AddSingleton<IApprovalRequestRepository>(sp => sp.GetRequiredService<InMemoryApprovalRequestRepository>());
        services.AddSingleton<InMemorySchedulingJobRepository>();
        services.AddSingleton<ISchedulingJobRepository>(sp => sp.GetRequiredService<InMemorySchedulingJobRepository>());
        services.AddSingleton<InMemoryConnectedPublishingProfileRepository>();
        services.AddSingleton<IConnectedPublishingProfileRepository>(sp => sp.GetRequiredService<InMemoryConnectedPublishingProfileRepository>());
        services.AddSingleton<InMemoryPublishedContentRecordRepository>();
        services.AddSingleton<IPublishedContentRecordRepository>(sp => sp.GetRequiredService<InMemoryPublishedContentRecordRepository>());
        services.AddSingleton<InMemoryLeadProfileRepository>();
        services.AddSingleton<ILeadProfileRepository>(sp => sp.GetRequiredService<InMemoryLeadProfileRepository>());
        services.AddSingleton<InMemoryManyChatContactStateRepository>();
        services.AddSingleton<IManyChatContactStateRepository>(sp => sp.GetRequiredService<InMemoryManyChatContactStateRepository>());
        services.AddSingleton<InMemoryBookingRecordRepository>();
        services.AddSingleton<IBookingRecordRepository>(sp => sp.GetRequiredService<InMemoryBookingRecordRepository>());
        services.AddSingleton<InMemoryReminderScheduleRepository>();
        services.AddSingleton<IReminderScheduleRepository>(sp => sp.GetRequiredService<InMemoryReminderScheduleRepository>());
        services.AddSingleton<InMemoryFollowUpSequenceRepository>();
        services.AddSingleton<IFollowUpSequenceRepository>(sp => sp.GetRequiredService<InMemoryFollowUpSequenceRepository>());
        services.AddSingleton<InMemoryVoiceCallSessionRepository>();
        services.AddSingleton<IVoiceCallSessionRepository>(sp => sp.GetRequiredService<InMemoryVoiceCallSessionRepository>());
        services.AddSingleton<InMemoryMonthlyPerformanceSnapshotRepository>();
        services.AddSingleton<IMonthlyPerformanceSnapshotRepository>(sp => sp.GetRequiredService<InMemoryMonthlyPerformanceSnapshotRepository>());
        services.AddSingleton<IMonthlyPerformanceReadService, InMemoryMonthlyPerformanceReadService>();
        services.AddSingleton<IVoiceConversationProvider, InMemoryElevenLabsVoiceConversationProvider>();
        return services;
    }

    private static IServiceCollection AddTableStorageInfrastructureAdapters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = TableStorageOptions.Resolve(configuration);
        services.AddSingleton(options);

        services.AddSingleton(sp => new TableServiceClient(options.ConnectionString));
        services.AddSingleton<TableStorageTenantRepository>();
        services.AddSingleton<ITenantRepository>(sp => sp.GetRequiredService<TableStorageTenantRepository>());
        services.AddSingleton<TableStorageTallySubmissionReceiptRepository>();
        services.AddSingleton<ITallySubmissionReceiptRepository>(sp => sp.GetRequiredService<TableStorageTallySubmissionReceiptRepository>());

        services.AddSingleton<TableStorageStrategyPlanRepository>();
        services.AddSingleton<IStrategyPlanRepository>(sp => sp.GetRequiredService<TableStorageStrategyPlanRepository>());

        services.AddSingleton<TableStorageContentMemoryRepository>();
        services.AddSingleton<IContentMemoryRepository>(sp => sp.GetRequiredService<TableStorageContentMemoryRepository>());

        services.AddSingleton<TableStorageVideoWebhookEndpointRegistrationRepository>();
        services.AddSingleton<IVideoWebhookEndpointRegistrationRepository>(sp => sp.GetRequiredService<TableStorageVideoWebhookEndpointRegistrationRepository>());

        services.AddSingleton<TableStorageEditorialBacklogRepository>();
        services.AddSingleton<IEditorialBacklogRepository>(sp => sp.GetRequiredService<TableStorageEditorialBacklogRepository>());

        return services;
    }

    private static IServiceCollection AddRemainingInMemoryInfrastructureAdapters(
        this IServiceCollection services,
        bool skipTenantSlice)
    {
        if (!skipTenantSlice)
        {
            services.AddSingleton<InMemoryTenantRepository>();
            services.AddSingleton<ITenantRepository>(sp => sp.GetRequiredService<InMemoryTenantRepository>());
            services.AddSingleton<InMemoryTallySubmissionReceiptRepository>();
            services.AddSingleton<ITallySubmissionReceiptRepository>(sp => sp.GetRequiredService<InMemoryTallySubmissionReceiptRepository>());
            services.AddSingleton<InMemoryStrategyPlanRepository>();
            services.AddSingleton<IStrategyPlanRepository>(sp => sp.GetRequiredService<InMemoryStrategyPlanRepository>());
            services.AddSingleton<InMemoryContentMemoryRepository>();
            services.AddSingleton<IContentMemoryRepository>(sp => sp.GetRequiredService<InMemoryContentMemoryRepository>());
            services.AddSingleton<InMemoryEditorialBacklogRepository>();
            services.AddSingleton<IEditorialBacklogRepository>(sp => sp.GetRequiredService<InMemoryEditorialBacklogRepository>());
        }

        services.AddSingleton<InMemoryDailyContentRequestRepository>();
        services.AddSingleton<IDailyContentRequestRepository>(sp => sp.GetRequiredService<InMemoryDailyContentRequestRepository>());
        services.AddSingleton<InMemoryDailyContentBriefRepository>();
        services.AddSingleton<IDailyContentBriefRepository>(sp => sp.GetRequiredService<InMemoryDailyContentBriefRepository>());
        services.AddSingleton<InMemoryPrimaryAssetRepository>();
        services.AddSingleton<IPrimaryAssetRepository>(sp => sp.GetRequiredService<InMemoryPrimaryAssetRepository>());
        services.AddSingleton<InMemoryCaptionAssetRepository>();
        services.AddSingleton<ICaptionAssetRepository>(sp => sp.GetRequiredService<InMemoryCaptionAssetRepository>());
        services.AddSingleton<InMemoryRepurposedAssetBundleRepository>();
        services.AddSingleton<IRepurposedAssetBundleRepository>(sp => sp.GetRequiredService<InMemoryRepurposedAssetBundleRepository>());
        services.AddSingleton<InMemoryVideoGenerationJobRepository>();
        services.AddSingleton<IVideoGenerationJobRepository>(sp => sp.GetRequiredService<InMemoryVideoGenerationJobRepository>());
        services.AddSingleton<InMemoryGeneratedVideoAssetRepository>();
        services.AddSingleton<IGeneratedVideoAssetRepository>(sp => sp.GetRequiredService<InMemoryGeneratedVideoAssetRepository>());
        services.AddSingleton<InMemoryVideoWebhookEndpointRegistrationRepository>();
        services.AddSingleton<IVideoWebhookEndpointRegistrationRepository>(sp => sp.GetRequiredService<InMemoryVideoWebhookEndpointRegistrationRepository>());
        services.AddSingleton<InMemoryComplianceReviewRepository>();
        services.AddSingleton<IComplianceReviewRepository>(sp => sp.GetRequiredService<InMemoryComplianceReviewRepository>());
        services.AddSingleton<InMemoryQualityReviewRepository>();
        services.AddSingleton<IQualityReviewRepository>(sp => sp.GetRequiredService<InMemoryQualityReviewRepository>());
        services.AddSingleton<InMemoryApprovalRequestRepository>();
        services.AddSingleton<IApprovalRequestRepository>(sp => sp.GetRequiredService<InMemoryApprovalRequestRepository>());
        services.AddSingleton<InMemorySchedulingJobRepository>();
        services.AddSingleton<ISchedulingJobRepository>(sp => sp.GetRequiredService<InMemorySchedulingJobRepository>());
        services.AddSingleton<InMemoryConnectedPublishingProfileRepository>();
        services.AddSingleton<IConnectedPublishingProfileRepository>(sp => sp.GetRequiredService<InMemoryConnectedPublishingProfileRepository>());
        services.AddSingleton<InMemoryPublishedContentRecordRepository>();
        services.AddSingleton<IPublishedContentRecordRepository>(sp => sp.GetRequiredService<InMemoryPublishedContentRecordRepository>());
        services.AddSingleton<InMemoryLeadProfileRepository>();
        services.AddSingleton<ILeadProfileRepository>(sp => sp.GetRequiredService<InMemoryLeadProfileRepository>());
        services.AddSingleton<InMemoryManyChatContactStateRepository>();
        services.AddSingleton<IManyChatContactStateRepository>(sp => sp.GetRequiredService<InMemoryManyChatContactStateRepository>());
        services.AddSingleton<InMemoryBookingRecordRepository>();
        services.AddSingleton<IBookingRecordRepository>(sp => sp.GetRequiredService<InMemoryBookingRecordRepository>());
        services.AddSingleton<InMemoryReminderScheduleRepository>();
        services.AddSingleton<IReminderScheduleRepository>(sp => sp.GetRequiredService<InMemoryReminderScheduleRepository>());
        services.AddSingleton<InMemoryFollowUpSequenceRepository>();
        services.AddSingleton<IFollowUpSequenceRepository>(sp => sp.GetRequiredService<InMemoryFollowUpSequenceRepository>());
        services.AddSingleton<InMemoryVoiceCallSessionRepository>();
        services.AddSingleton<IVoiceCallSessionRepository>(sp => sp.GetRequiredService<InMemoryVoiceCallSessionRepository>());
        services.AddSingleton<InMemoryMonthlyPerformanceSnapshotRepository>();
        services.AddSingleton<IMonthlyPerformanceSnapshotRepository>(sp => sp.GetRequiredService<InMemoryMonthlyPerformanceSnapshotRepository>());
        services.AddSingleton<IMonthlyPerformanceReadService, InMemoryMonthlyPerformanceReadService>();
        services.AddSingleton<IVoiceConversationProvider, InMemoryElevenLabsVoiceConversationProvider>();

        return services;
    }
}
