using AIMultiAgentPlatform.Application.Booking;
using AIMultiAgentPlatform.Application.DependencyInjection;
using AIMultiAgentPlatform.Application.DailyContent;
using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Application.LeadGeneration;
using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Application.ReviewAndScheduling;
using AIMultiAgentPlatform.Application.Voice;
using AIMultiAgentPlatform.Contracts.Booking;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Contracts.ManyChat;
using AIMultiAgentPlatform.Contracts.Reporting;
using AIMultiAgentPlatform.Contracts.Voice;
using AIMultiAgentPlatform.Infrastructure.DependencyInjection;
using AIMultiAgentPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AIMultiAgentPlatform.IntegrationTests;

public sealed class TallyIntakeFlowTests
{
    [Fact]
    public async Task IntakeFlow_PersistsTenantStrategyAndBacklog()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var useCase = scope.ServiceProvider.GetRequiredService<ProcessTallySubmissionUseCase>();

        var response = await useCase.ExecuteAsync(
            new ProcessTallySubmissionCommand(
                new TallySubmissionRequest(
                    "sub_100",
                    "RNM Studio",
                    "Jane Doe",
                    "jane@rnm.test",
                    "Agencies",
                    "AI content systems",
                    "Founders",
                    "Bold",
                    "BOOK",
                    ["Instagram", "LinkedIn"],
                    ["Low engagement", "Weak pipeline"],
                    ["No time", "Unsure what to post"],
                    ["Politics"],
                    14)),
            CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Value);

        var tenantRepository = (InMemoryTenantRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.ITenantRepository>();
        var strategyRepository = (InMemoryStrategyPlanRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IStrategyPlanRepository>();
        var backlogRepository = (InMemoryEditorialBacklogRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IEditorialBacklogRepository>();

        Assert.NotNull(tenantRepository.Find(response.Value!.TenantId));
        Assert.NotNull(strategyRepository.Find(response.Value.StrategyPlanId));
        Assert.Equal(14, backlogRepository.Find(response.Value.EditorialBacklogId)!.Items.Count);
    }

    [Fact]
    public async Task IntakeThenDailyContentFlow_GeneratesPersistedContentPackage()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var intakeUseCase = scope.ServiceProvider.GetRequiredService<ProcessTallySubmissionUseCase>();
        var dailyContentUseCase = scope.ServiceProvider.GetRequiredService<GenerateDailyContentPackageUseCase>();

        var intakeResponse = await intakeUseCase.ExecuteAsync(
            new ProcessTallySubmissionCommand(
                new TallySubmissionRequest(
                    "sub_101",
                    "RNM Studio",
                    "Jane Doe",
                    "jane@rnm.test",
                    "Agencies",
                    "AI content systems",
                    "Founders",
                    "Bold",
                    "BOOK",
                    ["Instagram", "LinkedIn", "TikTok"],
                    ["Low engagement", "Weak pipeline"],
                    ["No time", "Unsure what to post"],
                    ["Politics"],
                    14),
                "corr-intake"),
            CancellationToken.None);

        Assert.True(intakeResponse.IsSuccess);

        var dailyResponse = await dailyContentUseCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(
                new GenerateDailyContentPackageRequest(
                    intakeResponse.Value!.TenantId,
                    intakeResponse.Value.EditorialBacklogId,
                    3,
                    "corr-daily"),
                "corr-daily"),
            CancellationToken.None);

        Assert.True(dailyResponse.IsSuccess);
        Assert.NotNull(dailyResponse.Value);
        Assert.Equal("ShortVideo", dailyResponse.Value!.PrimaryFormat);

        var requestRepository = (InMemoryDailyContentRequestRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IDailyContentRequestRepository>();
        var primaryAssetRepository = (InMemoryPrimaryAssetRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IPrimaryAssetRepository>();
        var bundleRepository = (InMemoryRepurposedAssetBundleRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IRepurposedAssetBundleRepository>();

        Assert.NotNull(requestRepository.Find(dailyResponse.Value.DailyContentRequestId));
        Assert.Contains("HeyGen-compatible", primaryAssetRepository.Find(dailyResponse.Value.PrimaryAssetId)!.ProductionNotes);
        Assert.NotEmpty(bundleRepository.Find(dailyResponse.Value.RepurposedAssetBundleId)!.CommentHooks);
    }

    [Fact]
    public async Task IntakeDailyReviewAndSchedulingFlow_CompletesApprovedPath()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var intakeUseCase = scope.ServiceProvider.GetRequiredService<ProcessTallySubmissionUseCase>();
        var dailyContentUseCase = scope.ServiceProvider.GetRequiredService<GenerateDailyContentPackageUseCase>();
        var reviewUseCase = scope.ServiceProvider.GetRequiredService<ReviewAndScheduleDailyContentUseCase>();

        var intakeResponse = await intakeUseCase.ExecuteAsync(
            new ProcessTallySubmissionCommand(
                new TallySubmissionRequest(
                    "sub_102",
                    "RNM Studio",
                    "Jane Doe",
                    "jane@rnm.test",
                    "Agencies",
                    "AI content systems",
                    "Founders",
                    "Bold",
                    "BOOK",
                    ["Instagram", "LinkedIn", "TikTok"],
                    ["Low engagement", "Weak pipeline"],
                    ["No time", "Unsure what to post"],
                    ["Politics"],
                    14),
                "corr-intake"),
            CancellationToken.None);

        var dailyResponse = await dailyContentUseCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(
                new GenerateDailyContentPackageRequest(
                    intakeResponse.Value!.TenantId,
                    intakeResponse.Value.EditorialBacklogId,
                    3,
                    "corr-daily"),
                "corr-daily"),
            CancellationToken.None);

        var reviewResponse = await reviewUseCase.ExecuteAsync(
            new ReviewAndScheduleDailyContentCommand(
                new ReviewAndScheduleDailyContentRequest(
                    intakeResponse.Value.TenantId,
                    dailyResponse.Value!.DailyContentRequestId,
                    "corr-review"),
                "corr-review"),
            CancellationToken.None);

        Assert.True(reviewResponse.IsSuccess);
        Assert.Equal("Approved", reviewResponse.Value!.ApprovalStatus);
        Assert.Equal("Scheduled", reviewResponse.Value.SchedulingStatus);
        Assert.Equal(3, reviewResponse.Value.ScheduledTargetCount);

        var schedulingRepository = (InMemorySchedulingJobRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.ISchedulingJobRepository>();
        var schedulingJob = await schedulingRepository.FindByRequestIdAsync(dailyResponse.Value.DailyContentRequestId, CancellationToken.None);
        Assert.Equal("Scheduled", schedulingJob!.Status.ToString());
    }

    [Fact]
    public async Task IntakeThenManyChatEventFlow_UpdatesLeadState()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var intakeUseCase = scope.ServiceProvider.GetRequiredService<ProcessTallySubmissionUseCase>();
        var manyChatUseCase = scope.ServiceProvider.GetRequiredService<ProcessManyChatEventUseCase>();

        var intakeResponse = await intakeUseCase.ExecuteAsync(
            new ProcessTallySubmissionCommand(
                new TallySubmissionRequest(
                    "sub_103",
                    "RNM Studio",
                    "Jane Doe",
                    "jane@rnm.test",
                    "Agencies",
                    "AI content systems",
                    "Founders",
                    "Bold",
                    "BOOK",
                    ["Instagram", "LinkedIn"],
                    ["Low engagement"],
                    ["No time"],
                    ["Politics"],
                    14),
                "corr-intake"),
            CancellationToken.None);

        var manyChatResponse = await manyChatUseCase.ExecuteAsync(
            new ProcessManyChatEventCommand(
                new ProcessManyChatEventRequest(
                    intakeResponse.Value!.TenantId,
                    "contact_200",
                    "message_received",
                    "instagram",
                    "Can you send me BOOK?",
                    "Jane",
                    "Doe",
                    "jane@rnm.test",
                    ["existing-tag"],
                    new Dictionary<string, string> { ["source"] = "instagram" },
                    "corr-manychat"),
                "corr-manychat"),
            CancellationToken.None);

        Assert.True(manyChatResponse.IsSuccess);
        Assert.Equal("MarketingQualified", manyChatResponse.Value!.LeadLifecycleStage);
        Assert.Equal("leadgen-keyword-capture", manyChatResponse.Value.TriggeredFlow);

        var leadRepository = (InMemoryLeadProfileRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.ILeadProfileRepository>();
        var manyChatStateRepository = (InMemoryManyChatContactStateRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IManyChatContactStateRepository>();

        Assert.Equal("MarketingQualified", leadRepository.Find(intakeResponse.Value.TenantId, "contact_200")!.CurrentStage.ToString());
        Assert.Equal("leadgen-keyword-capture", manyChatStateRepository.Find(intakeResponse.Value.TenantId, "contact_200")!.TriggeredFlow);
    }

    [Fact]
    public async Task IntakeManyChatAndBookingFlow_CreatesReminderScheduleWhenBooked()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var intakeUseCase = scope.ServiceProvider.GetRequiredService<ProcessTallySubmissionUseCase>();
        var manyChatUseCase = scope.ServiceProvider.GetRequiredService<ProcessManyChatEventUseCase>();
        var bookingUseCase = scope.ServiceProvider.GetRequiredService<OrchestrateBookingAgentUseCase>();

        var intakeResponse = await intakeUseCase.ExecuteAsync(
            new ProcessTallySubmissionCommand(
                new TallySubmissionRequest(
                    "sub_104",
                    "RNM Studio",
                    "Jane Doe",
                    "jane@rnm.test",
                    "Agencies",
                    "AI content systems",
                    "Founders",
                    "Bold",
                    "BOOK",
                    ["Instagram", "LinkedIn"],
                    ["Low engagement"],
                    ["No time"],
                    ["Politics"],
                    14),
                "corr-intake"),
            CancellationToken.None);

        await manyChatUseCase.ExecuteAsync(
            new ProcessManyChatEventCommand(
                new ProcessManyChatEventRequest(
                    intakeResponse.Value!.TenantId,
                    "contact_300",
                    "message_received",
                    "instagram",
                    "I want to book a call",
                    "Jane",
                    "Doe",
                    "jane@rnm.test"),
                "corr-manychat"),
            CancellationToken.None);

        var appointmentUtc = new DateTime(2026, 03, 26, 17, 0, 0, DateTimeKind.Utc);
        var bookingResponse = await bookingUseCase.ExecuteAsync(
            new OrchestrateBookingAgentCommand(
                new OrchestrateBookingAgentRequest(
                    intakeResponse.Value.TenantId,
                    "contact_300",
                    "Booked",
                    appointmentUtc,
                    "strategy-call",
                    ["Email", "Instagram"],
                    "corr-booking"),
                "corr-booking"),
            CancellationToken.None);

        Assert.True(bookingResponse.IsSuccess);
        Assert.Equal("Booked", bookingResponse.Value!.BookingStatus);
        Assert.NotNull(bookingResponse.Value.ReminderScheduleId);

        var reminderRepository = (InMemoryReminderScheduleRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IReminderScheduleRepository>();
        var bookingRepository = (InMemoryBookingRecordRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IBookingRecordRepository>();

        var bookingRecord = bookingRepository.Find(intakeResponse.Value.TenantId, "contact_300");
        Assert.NotNull(bookingRecord);
        Assert.Equal("Booked", bookingRecord!.Status.ToString());
        Assert.Equal(4, reminderRepository.Find(bookingRecord.BookingRecordId)!.Touches.Count);
    }

    [Fact]
    public async Task FullFlow_CanGenerateMonthlyPerformanceSnapshot()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var intakeUseCase = scope.ServiceProvider.GetRequiredService<ProcessTallySubmissionUseCase>();
        var dailyContentUseCase = scope.ServiceProvider.GetRequiredService<GenerateDailyContentPackageUseCase>();
        var reviewUseCase = scope.ServiceProvider.GetRequiredService<ReviewAndScheduleDailyContentUseCase>();
        var manyChatUseCase = scope.ServiceProvider.GetRequiredService<ProcessManyChatEventUseCase>();
        var bookingUseCase = scope.ServiceProvider.GetRequiredService<OrchestrateBookingAgentUseCase>();
        var reportUseCase = scope.ServiceProvider.GetRequiredService<GenerateMonthlyPerformanceSnapshotUseCase>();

        var intakeResponse = await intakeUseCase.ExecuteAsync(
            new ProcessTallySubmissionCommand(
                new TallySubmissionRequest(
                    "sub_105",
                    "RNM Studio",
                    "Jane Doe",
                    "jane@rnm.test",
                    "Agencies",
                    "AI content systems",
                    "Founders",
                    "Bold",
                    "BOOK",
                    ["Instagram", "LinkedIn"],
                    ["Low engagement"],
                    ["No time"],
                    ["Politics"],
                    14),
                "corr-intake"),
            CancellationToken.None);

        var dailyResponse = await dailyContentUseCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(
                new GenerateDailyContentPackageRequest(
                    intakeResponse.Value!.TenantId,
                    intakeResponse.Value.EditorialBacklogId,
                    3,
                    "corr-daily"),
                "corr-daily"),
            CancellationToken.None);

        await reviewUseCase.ExecuteAsync(
            new ReviewAndScheduleDailyContentCommand(
                new ReviewAndScheduleDailyContentRequest(
                    intakeResponse.Value.TenantId,
                    dailyResponse.Value!.DailyContentRequestId,
                    "corr-review"),
                "corr-review"),
            CancellationToken.None);

        await manyChatUseCase.ExecuteAsync(
            new ProcessManyChatEventCommand(
                new ProcessManyChatEventRequest(
                    intakeResponse.Value.TenantId,
                    "contact_400",
                    "message_received",
                    "instagram",
                    "I want to book a call",
                    "Jane",
                    "Doe",
                    "jane@rnm.test"),
                "corr-manychat"),
            CancellationToken.None);

        await bookingUseCase.ExecuteAsync(
            new OrchestrateBookingAgentCommand(
                new OrchestrateBookingAgentRequest(
                    intakeResponse.Value.TenantId,
                    "contact_400",
                    "Booked",
                    new DateTime(2026, 03, 27, 17, 0, 0, DateTimeKind.Utc),
                    "strategy-call",
                    ["Email", "Instagram"],
                    "corr-booking"),
                "corr-booking"),
            CancellationToken.None);

        var reportResponse = await reportUseCase.ExecuteAsync(
            new GenerateMonthlyPerformanceSnapshotCommand(
                new GenerateMonthlyPerformanceSnapshotRequest(
                    intakeResponse.Value.TenantId,
                    2026,
                    3,
                    "corr-report"),
                "corr-report"),
            CancellationToken.None);

        Assert.True(reportResponse.IsSuccess);
        Assert.Equal("2026-03", reportResponse.Value!.MonthKey);
        Assert.Equal(2, reportResponse.Value.PostsPublished);
        Assert.Equal(1, reportResponse.Value.VideosCreated);
        Assert.Equal(1, reportResponse.Value.AppointmentsBooked);
        Assert.Equal(4, reportResponse.Value.ReminderTouchesScheduled);
    }

    [Fact]
    public async Task IntakeManyChatAndVoiceFlow_CreatesVoiceBookingAndReminderArtifacts()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var intakeUseCase = scope.ServiceProvider.GetRequiredService<ProcessTallySubmissionUseCase>();
        var manyChatUseCase = scope.ServiceProvider.GetRequiredService<ProcessManyChatEventUseCase>();
        var voiceUseCase = scope.ServiceProvider.GetRequiredService<OrchestrateVoiceAgentUseCase>();

        var intakeResponse = await intakeUseCase.ExecuteAsync(
            new ProcessTallySubmissionCommand(
                new TallySubmissionRequest(
                    "sub_106",
                    "RNM Studio",
                    "Jane Doe",
                    "jane@rnm.test",
                    "Agencies",
                    "AI content systems",
                    "Founders",
                    "Bold",
                    "BOOK",
                    ["Instagram", "LinkedIn"],
                    ["Low engagement"],
                    ["No time"],
                    ["Politics"],
                    14),
                "corr-intake"),
            CancellationToken.None);

        await manyChatUseCase.ExecuteAsync(
            new ProcessManyChatEventCommand(
                new ProcessManyChatEventRequest(
                    intakeResponse.Value!.TenantId,
                    "contact_500",
                    "message_received",
                    "instagram",
                    "I want to schedule a call",
                    "Jane",
                    "Doe",
                    "jane@rnm.test"),
                "corr-manychat"),
            CancellationToken.None);

        var voiceResponse = await voiceUseCase.ExecuteAsync(
            new OrchestrateVoiceAgentCommand(
                new OrchestrateVoiceAgentRequest(
                    intakeResponse.Value.TenantId,
                    "contact_500",
                    "Booking",
                    "+12145550111",
                    "rachel",
                    "corr-voice"),
                "corr-voice"),
            CancellationToken.None);

        Assert.True(voiceResponse.IsSuccess);
        Assert.Equal("Booked", voiceResponse.Value!.CallDisposition);
        Assert.Equal("Booked", voiceResponse.Value.LeadLifecycleStage);
        Assert.NotNull(voiceResponse.Value.BookingRecordId);
        Assert.NotNull(voiceResponse.Value.ReminderScheduleId);

        var bookingRepository = (InMemoryBookingRecordRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IBookingRecordRepository>();
        var reminderRepository = (InMemoryReminderScheduleRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IReminderScheduleRepository>();
        var voiceRepository = (InMemoryVoiceCallSessionRepository)scope.ServiceProvider.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.IVoiceCallSessionRepository>();

        var bookingRecord = bookingRepository.Find(intakeResponse.Value.TenantId, "contact_500");
        Assert.NotNull(bookingRecord);
        Assert.Equal("Booked", bookingRecord!.Status.ToString());
        Assert.Equal(2, reminderRepository.Find(bookingRecord.BookingRecordId)!.Touches.Count);
        Assert.Equal("Booked", voiceRepository.Find(voiceResponse.Value.VoiceCallSessionId)!.Disposition.ToString());
    }
}
