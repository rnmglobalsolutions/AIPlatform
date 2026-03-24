using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Abstractions.Voice;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Persistence;
using AIMultiAgentPlatform.Infrastructure.Reporting;
using AIMultiAgentPlatform.Infrastructure.Voice;
using Microsoft.Extensions.DependencyInjection;

namespace AIMultiAgentPlatform.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
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
            PlatformMode.Lean => services.AddLeanInfrastructure(settings),
            PlatformMode.Production => services.AddProductionInfrastructure(settings),
            _ => services.AddLeanInfrastructure(settings)
        };
    }

    private static IServiceCollection AddSharedInfrastructureCore(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();
        return services;
    }

    private static IServiceCollection AddLeanInfrastructure(this IServiceCollection services, InfrastructureModeSettings settings) =>
        services.AddInMemoryInfrastructureAdapters();

    private static IServiceCollection AddProductionInfrastructure(this IServiceCollection services, InfrastructureModeSettings settings) =>
        services.AddInMemoryInfrastructureAdapters();

    private static IServiceCollection AddInMemoryInfrastructureAdapters(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryTenantRepository>();
        services.AddSingleton<ITenantRepository>(sp => sp.GetRequiredService<InMemoryTenantRepository>());
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
}
