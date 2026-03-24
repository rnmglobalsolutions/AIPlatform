using Microsoft.Extensions.DependencyInjection;

namespace AIMultiAgentPlatform.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Intake.ProcessTallySubmissionUseCase>();
        services.AddScoped<DailyContent.GenerateDailyContentPackageUseCase>();
        services.AddScoped<ReviewAndScheduling.ReviewAndScheduleDailyContentUseCase>();
        services.AddScoped<LeadGeneration.ProcessManyChatEventUseCase>();
        services.AddScoped<Booking.OrchestrateBookingAgentUseCase>();
        services.AddScoped<Reporting.GenerateMonthlyPerformanceSnapshotUseCase>();
        services.AddScoped<Voice.OrchestrateVoiceAgentUseCase>();
        return services;
    }
}
