using Microsoft.Extensions.DependencyInjection;

namespace AIMultiAgentPlatform.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Content.GenerateCanonicalContentFrameUseCase>();
        services.AddScoped<Content.RefreshRepurposedAssetBundleFromVideoUseCase>();
        services.AddScoped<Reviewing.EvaluateGeneratedContentUseCase>();
        services.AddScoped<Strategy.BuildStrategicProfileUseCase>();
        services.AddScoped<Strategy.GenerateStrategyBlueprintUseCase>();
        services.AddScoped<Intake.ProcessTallySubmissionUseCase>();
        services.AddScoped<Intake.EnqueueProcessTallySubmissionUseCase>();
        services.AddScoped<DailyContent.GenerateDailyContentPackageUseCase>();
        services.AddScoped<DailyContent.EnqueueGenerateDailyContentPackageUseCase>();
        services.AddScoped<ReviewAndScheduling.EnqueueReviewAndScheduleDailyContentUseCase>();
        services.AddScoped<Video.RequestVideoGenerationUseCase>();
        services.AddScoped<Video.FinalizeVideoGenerationUseCase>();
        services.AddScoped<Video.EnsureHeyGenWebhookEndpointUseCase>();
        services.AddScoped<Video.GetHeyGenWebhookEndpointUseCase>();
        services.AddScoped<Video.DeleteHeyGenWebhookEndpointUseCase>();
        services.AddScoped<Video.ProcessHeyGenWebhookUseCase>();
        services.AddScoped<Video.ProcessPendingVideoGenerationJobsUseCase>();
        services.AddScoped<Abstractions.Video.IVideoGenerationJobFinalizer, Video.VideoGenerationJobFinalizer>();
        services.AddScoped<Publishing.UpsertConnectedPublishingProfileUseCase>();
        services.AddScoped<Publishing.ListConnectedPublishingProfilesUseCase>();
        services.AddScoped<Publishing.PublishScheduledContentUseCase>();
        services.AddScoped<Publishing.EnqueuePublishScheduledContentUseCase>();
        services.AddScoped<Publishing.ReconcilePublishedContentUseCase>();
        services.AddScoped<Abstractions.Publishing.IPublishingProviderSelector, Publishing.PublishingProviderSelector>();
        services.AddScoped<ReviewAndScheduling.ReviewAndScheduleDailyContentUseCase>();
        services.AddScoped<Orchestration.DispatchPendingOutboxCommandsUseCase>();
        services.AddScoped<LeadGeneration.ProcessManyChatEventUseCase>();
        services.AddScoped<Booking.OrchestrateBookingAgentUseCase>();
        services.AddScoped<Reporting.GenerateMonthlyPerformanceSnapshotUseCase>();
        services.AddScoped<Reporting.GenerateExecutiveReportUseCase>();
        services.AddScoped<Reporting.GenerateOperationalReportUseCase>();
        services.AddScoped<Reporting.GenerateOptimizationRecommendationsUseCase>();
        services.AddScoped<Voice.OrchestrateVoiceAgentUseCase>();
        return services;
    }
}
