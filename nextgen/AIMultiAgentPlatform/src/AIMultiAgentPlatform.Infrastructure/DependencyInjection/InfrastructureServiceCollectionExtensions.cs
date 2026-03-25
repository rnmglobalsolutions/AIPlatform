using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Abstractions.Voice;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Persistence;
using AIMultiAgentPlatform.Infrastructure.Persistence.TableStorage;
using AIMultiAgentPlatform.Infrastructure.Reporting;
using AIMultiAgentPlatform.Infrastructure.Voice;
using Azure.Data.Tables;
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
        services.AddSharedInfrastructureCore();

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

        services.AddSharedInfrastructureCore();

        return settings.PlatformMode switch
        {
            PlatformMode.Lean => services.AddLeanInfrastructure(settings, configuration: null),
            PlatformMode.Production => services.AddProductionInfrastructure(settings, configuration: null),
            _ => services.AddLeanInfrastructure(settings, configuration: null)
        };
    }

    private static IServiceCollection AddSharedInfrastructureCore(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();
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
        if (settings.PersistenceMode == PersistenceMode.Sql)
        {
            throw new NotSupportedException("SQL persistence wiring has not been implemented yet. Use InMemory for production scaffolding or finish the SQL adapters first.");
        }

        return services.AddInMemoryInfrastructureAdapters();
    }

    private static IServiceCollection AddInMemoryInfrastructureAdapters(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryTenantRepository>();
        services.AddSingleton<ITenantRepository>(sp => sp.GetRequiredService<InMemoryTenantRepository>());
        services.AddSingleton<InMemoryTallySubmissionReceiptRepository>();
        services.AddSingleton<ITallySubmissionReceiptRepository>(sp => sp.GetRequiredService<InMemoryTallySubmissionReceiptRepository>());
        services.AddSingleton<InMemoryStrategyPlanRepository>();
        services.AddSingleton<IStrategyPlanRepository>(sp => sp.GetRequiredService<InMemoryStrategyPlanRepository>());
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
        services.AddSingleton<InMemoryComplianceReviewRepository>();
        services.AddSingleton<IComplianceReviewRepository>(sp => sp.GetRequiredService<InMemoryComplianceReviewRepository>());
        services.AddSingleton<InMemoryQualityReviewRepository>();
        services.AddSingleton<IQualityReviewRepository>(sp => sp.GetRequiredService<InMemoryQualityReviewRepository>());
        services.AddSingleton<InMemoryApprovalRequestRepository>();
        services.AddSingleton<IApprovalRequestRepository>(sp => sp.GetRequiredService<InMemoryApprovalRequestRepository>());
        services.AddSingleton<InMemorySchedulingJobRepository>();
        services.AddSingleton<ISchedulingJobRepository>(sp => sp.GetRequiredService<InMemorySchedulingJobRepository>());
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
        services.AddSingleton<InMemoryComplianceReviewRepository>();
        services.AddSingleton<IComplianceReviewRepository>(sp => sp.GetRequiredService<InMemoryComplianceReviewRepository>());
        services.AddSingleton<InMemoryQualityReviewRepository>();
        services.AddSingleton<IQualityReviewRepository>(sp => sp.GetRequiredService<InMemoryQualityReviewRepository>());
        services.AddSingleton<InMemoryApprovalRequestRepository>();
        services.AddSingleton<IApprovalRequestRepository>(sp => sp.GetRequiredService<InMemoryApprovalRequestRepository>());
        services.AddSingleton<InMemorySchedulingJobRepository>();
        services.AddSingleton<ISchedulingJobRepository>(sp => sp.GetRequiredService<InMemorySchedulingJobRepository>());
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
