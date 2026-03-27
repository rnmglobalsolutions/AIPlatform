using AIMultiAgentPlatform.Domain.Booking;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Communications;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.FollowUps;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Reminders;
using AIMultiAgentPlatform.Domain.Reviewing;
using AIMultiAgentPlatform.Domain.Strategy;
using AIMultiAgentPlatform.Domain.Tenants;
using AIMultiAgentPlatform.Infrastructure.Persistence;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class InMemoryRepositoriesTests
{
    [Fact]
    public async Task InMemoryRepositories_SaveAndExposePersistedEntities()
    {
        var tenantRepository = new InMemoryTenantRepository();
        var strategyRepository = new InMemoryStrategyPlanRepository();
        var contentMemoryRepository = new InMemoryContentMemoryRepository();
        var backlogRepository = new InMemoryEditorialBacklogRepository();
        var requestRepository = new InMemoryDailyContentRequestRepository();
        var briefRepository = new InMemoryDailyContentBriefRepository();
        var primaryAssetRepository = new InMemoryPrimaryAssetRepository();
        var captionAssetRepository = new InMemoryCaptionAssetRepository();
        var bundleRepository = new InMemoryRepurposedAssetBundleRepository();
        var videoJobRepository = new InMemoryVideoGenerationJobRepository();
        var generatedVideoAssetRepository = new InMemoryGeneratedVideoAssetRepository();
        var webhookRegistrationRepository = new InMemoryVideoWebhookEndpointRegistrationRepository();
        var complianceRepository = new InMemoryComplianceReviewRepository();
        var qualityRepository = new InMemoryQualityReviewRepository();
        var approvalRepository = new InMemoryApprovalRequestRepository();
        var schedulingRepository = new InMemorySchedulingJobRepository();
        var connectedPublishingProfileRepository = new InMemoryConnectedPublishingProfileRepository();
        var publishedContentRecordRepository = new InMemoryPublishedContentRecordRepository();
        var leadRepository = new InMemoryLeadProfileRepository();
        var manyChatStateRepository = new InMemoryManyChatContactStateRepository();
        var bookingRepository = new InMemoryBookingRecordRepository();
        var reminderRepository = new InMemoryReminderScheduleRepository();
        var followUpRepository = new InMemoryFollowUpSequenceRepository();
        var tenantId = new TenantId("tenant_123");

        var tenant = Tenant.Create(
            tenantId,
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "Consultants",
                "Content-led growth",
                "Founders",
                "Confident",
                "BOOK",
                ["Instagram"],
                ["Low visibility"],
                ["No time"],
                []),
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

        var strategy = new StrategyPlan("strategy_123", tenantId, "Narrative", ["Authority"], 1, 2, DateTime.UtcNow, ContentPlanTier.Growth, 16);
        var backlog = new EditorialBacklog(
            "backlog_123",
            tenantId,
            14,
            DateTime.UtcNow,
            [new EditorialBacklogItem(1, 0, ContentCategory.Authority, PrimaryFormat.BrandedGraphic, "Topic", "Angle", "Hook", "comments_or_dms", true)]);
        var request = new DailyContentRequest("daily_request_123", tenantId, "backlog_123", 1, DateTime.UtcNow, "corr-123");
        var brief = new DailyContentBrief("brief_123", "daily_request_123", tenantId, ContentCategory.Authority, PrimaryFormat.BrandedGraphic, "Topic", "Angle", "Hook", "Core message", "BOOK", "Bold");
        var primaryAsset = new PrimaryAsset("primary_asset_123", "daily_request_123", tenantId, PrimaryFormat.BrandedGraphic, "Headline", "Hook", "Body", "Payoff", "CTA", "Notes");
        var captionAsset = new CaptionAsset("caption_123", "daily_request_123", "Caption", "Engagement", "BOOK", ["#Consultants"]);
        var bundle = new RepurposedAssetBundle("repurpose_123", "daily_request_123", "Carousel", ["Frame 1", "Frame 2"], "LinkedIn", "Quote", "Clip", ["Hook 1"]);
        var videoJob = new VideoGenerationJob("video_job_123", "daily_request_123", tenantId, "primary_asset_123", "FakeVideoProvider", "default", "provider_job_123", "Headline", "Script", "English", "9:16", VideoGenerationJobStatus.Completed, string.Empty, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
        var generatedVideoAsset = new GeneratedVideoAsset("video_asset_123", "video_job_123", "daily_request_123", tenantId, "primary_asset_123", "FakeVideoProvider", "provider_job_123", "Headline", "https://video.test/source.mp4", "https://video.test/final.mp4", "Transcript", "English", "9:16", DateTime.UtcNow);
        var webhookRegistration = new VideoWebhookEndpointRegistration("HeyGen", "endpoint_123", "https://api.test/heygen/webhook", "enabled", ["avatar_video.success"], "secret_123", DateTime.UtcNow, DateTime.UtcNow);
        var compliance = new ComplianceReview("compliance_123", "daily_request_123", tenantId, RiskLevel.Low, [], "Safe", DateTime.UtcNow);
        var quality = new QualityReview("quality_123", "daily_request_123", tenantId, 8.0, 8.1, 8.2, 8.3, 8.15, "Feedback", "Optimized CTA", DateTime.UtcNow);
        var approval = new ApprovalRequest("approval_123", "daily_request_123", tenantId, ApprovalStatus.Approved, "Looks good", DateTime.UtcNow);
        var scheduling = new SchedulingJob("schedule_123", "daily_request_123", tenantId, SchedulingStatus.Scheduled, "Scheduled", DateTime.UtcNow, [new PublicationTarget("Instagram", DateTime.UtcNow, "Payload")]);
        var connectedPublishingProfile = new ConnectedPublishingProfile("publish_profile_123", tenantId, "Buffer", "Instagram", "profile_123", "publish_secret_123", "RNM Instagram", DateTime.UtcNow, DateTime.UtcNow);
        var publishedRecord = new PublishedContentRecord("published_123", "daily_request_123", "schedule_123", tenantId, "Buffer", "Instagram", "profile_123", "post_123", "", "Caption", "https://blob.test/video.mp4", PublishedContentStatus.Published, string.Empty, DateTime.UtcNow);
        var lead = new LeadProfile("lead_123", tenantId, "contact_123", "Jane", "Doe", "jane@rnm.test", "Instagram", LeadLifecycleStage.MarketingQualified, "Keyword capture", "BOOK", DateTime.UtcNow);
        var manyChatState = new ManyChatContactState("mc_123", tenantId, "contact_123", ["leadgen-keyword"], new Dictionary<string, string> { ["lead_stage"] = "MarketingQualified" }, "BOOK", "leadgen-keyword-capture", DateTime.UtcNow);
        var booking = new BookingRecord("booking_123", tenantId, "lead_123", "contact_123", BookingStatus.Booked, "https://calendly.test/rnm", "strategy-call", DateTime.UtcNow.AddDays(2), DateTime.UtcNow);
        var reminder = new ReminderSchedule("reminder_123", tenantId, "booking_123", ReminderScheduleStatus.Scheduled, [new ReminderTouch(CommunicationChannel.Email, DateTime.UtcNow.AddHours(1), "appointment-reminder-1h")], DateTime.UtcNow);
        var followUp = new FollowUpSequence("followup_123", tenantId, "lead_123", FollowUpSequenceStatus.Scheduled, "No booking", [new FollowUpStep(CommunicationChannel.Instagram, DateTime.UtcNow.AddDays(1), "follow-up-day-1")], DateTime.UtcNow);
        var memoryEntry = new ContentMemoryEntry(
            "memory_123",
            tenantId,
            "PrimaryAsset",
            "primary_asset_123",
            "Topic",
            "Hook",
            "BOOK",
            "comment_keyword",
            "Instagram",
            "hash_123",
            DateTime.UtcNow,
            ContentMemoryLifecycleStage.Generated);

        await tenantRepository.SaveAsync(tenant, CancellationToken.None);
        await strategyRepository.SaveAsync(strategy, CancellationToken.None);
        await contentMemoryRepository.SaveAsync(memoryEntry, CancellationToken.None);
        await backlogRepository.SaveAsync(backlog, CancellationToken.None);
        await requestRepository.SaveAsync(request, CancellationToken.None);
        await briefRepository.SaveAsync(brief, CancellationToken.None);
        await primaryAssetRepository.SaveAsync(primaryAsset, CancellationToken.None);
        await captionAssetRepository.SaveAsync(captionAsset, CancellationToken.None);
        await bundleRepository.SaveAsync(bundle, CancellationToken.None);
        await videoJobRepository.SaveAsync(videoJob, CancellationToken.None);
        await generatedVideoAssetRepository.SaveAsync(generatedVideoAsset, CancellationToken.None);
        await webhookRegistrationRepository.SaveAsync(webhookRegistration, CancellationToken.None);
        await complianceRepository.SaveAsync(compliance, CancellationToken.None);
        await qualityRepository.SaveAsync(quality, CancellationToken.None);
        await approvalRepository.SaveAsync(approval, CancellationToken.None);
        await schedulingRepository.SaveAsync(scheduling, CancellationToken.None);
        await connectedPublishingProfileRepository.SaveAsync(connectedPublishingProfile, CancellationToken.None);
        await publishedContentRecordRepository.SaveAsync(publishedRecord, CancellationToken.None);
        await leadRepository.SaveAsync(lead, CancellationToken.None);
        await manyChatStateRepository.SaveAsync(manyChatState, CancellationToken.None);
        await bookingRepository.SaveAsync(booking, CancellationToken.None);
        await reminderRepository.SaveAsync(reminder, CancellationToken.None);
        await followUpRepository.SaveAsync(followUp, CancellationToken.None);

        Assert.Equal("rnm-growth", tenantRepository.Find("tenant_123")!.Slug);
        Assert.Equal("strategy_123", strategyRepository.Find("strategy_123")!.StrategyPlanId);
        Assert.Equal(ContentPlanTier.Growth, strategyRepository.Find("strategy_123")!.ContentPlanTier);
        Assert.Equal(16, strategyRepository.Find("strategy_123")!.MonthlyVideoTarget);
        Assert.Equal("memory_123", contentMemoryRepository.Find("tenant_123", "memory_123")!.ContentMemoryEntryId);
        Assert.Single(backlogRepository.Find("backlog_123")!.Items);
        Assert.Equal("daily_request_123", requestRepository.Find("daily_request_123")!.DailyContentRequestId);
        Assert.Equal("brief_123", briefRepository.Find("brief_123")!.DailyContentBriefId);
        Assert.Equal("primary_asset_123", primaryAssetRepository.Find("primary_asset_123")!.PrimaryAssetId);
        Assert.Equal("caption_123", captionAssetRepository.Find("caption_123")!.CaptionAssetId);
        Assert.Equal("repurpose_123", bundleRepository.Find("repurpose_123")!.RepurposedAssetBundleId);
        Assert.Equal("video_job_123", videoJobRepository.Find("video_job_123")!.VideoGenerationJobId);
        Assert.Equal("video_asset_123", generatedVideoAssetRepository.Find("video_asset_123")!.GeneratedVideoAssetId);
        Assert.Equal("endpoint_123", webhookRegistrationRepository.Find("HeyGen")!.EndpointId);
        Assert.Equal("compliance_123", complianceRepository.Find("compliance_123")!.ComplianceReviewId);
        Assert.Equal("quality_123", qualityRepository.Find("quality_123")!.QualityReviewId);
        Assert.Equal("approval_123", approvalRepository.Find("approval_123")!.ApprovalRequestId);
        Assert.Equal("schedule_123", schedulingRepository.Find("schedule_123")!.SchedulingJobId);
        Assert.Equal("publish_profile_123", (await connectedPublishingProfileRepository.FindByTenantAndPlatformAsync("tenant_123", "Instagram", CancellationToken.None))!.ConnectedPublishingProfileId);
        Assert.Equal("publish_profile_123", (await connectedPublishingProfileRepository.FindByTenantPlatformAndProviderAsync("tenant_123", "Instagram", "Buffer", CancellationToken.None))!.ConnectedPublishingProfileId);
        Assert.Equal("published_123", (await publishedContentRecordRepository.FindByRequestIdAsync("daily_request_123", CancellationToken.None)).Single().PublishedContentRecordId);
        Assert.Equal("lead_123", leadRepository.Find("tenant_123", "contact_123")!.LeadProfileId);
        Assert.Equal("mc_123", manyChatStateRepository.Find("tenant_123", "contact_123")!.ManyChatContactStateId);
        Assert.Equal("booking_123", bookingRepository.Find("tenant_123", "contact_123")!.BookingRecordId);
        Assert.Equal("reminder_123", reminderRepository.Find("booking_123")!.ReminderScheduleId);
        Assert.Equal("followup_123", followUpRepository.Find("lead_123")!.FollowUpSequenceId);
    }

    [Fact]
    public async Task InMemoryContentMemoryRepository_BuildsOrderedDistinctSnapshot()
    {
        var repository = new InMemoryContentMemoryRepository();
        var tenantId = new TenantId("tenant_memory");
        var createdUtc = new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc);

        await repository.SaveAsync(
            new ContentMemoryEntry(
                "memory_1",
                tenantId,
                "PrimaryAsset",
                "asset_1",
                "Lead gen myths",
                "Stop posting like this",
                "BOOK",
                "comment_keyword",
                "Instagram",
                "hash_1",
                createdUtc,
                ContentMemoryLifecycleStage.Published,
                createdUtc.AddMinutes(10)),
            CancellationToken.None);

        await repository.SaveAsync(
            new ContentMemoryEntry(
                "memory_2",
                tenantId,
                "PrimaryAsset",
                "asset_2",
                "Lead gen myths",
                "Stop posting like this",
                "DM",
                "send_dm",
                "LinkedIn",
                "hash_2",
                createdUtc.AddMinutes(1),
                ContentMemoryLifecycleStage.Generated,
                createdUtc.AddMinutes(20)),
            CancellationToken.None);

        var snapshot = await repository.GetSnapshotAsync(tenantId.Value, 10, CancellationToken.None);

        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Equal("memory_2", snapshot.Entries[0].ContentMemoryEntryId);
        Assert.Single(snapshot.RecentTopics);
        Assert.Contains("Lead gen myths", snapshot.RecentTopics);
        Assert.Single(snapshot.RecentGeneratedTopics);
        Assert.Single(snapshot.RecentPublishedTopics);
        Assert.Contains("Lead gen myths", snapshot.RecentPublishedTopics);
        Assert.Single(snapshot.RecentHooks);
        Assert.Single(snapshot.RecentGeneratedHooks);
        Assert.Single(snapshot.RecentPublishedHooks);
        Assert.Contains("DM", snapshot.RecentCallToActionPatterns);
        Assert.Contains("BOOK", snapshot.RecentCallToActionPatterns);
        Assert.Contains("Instagram", snapshot.RecentPlatforms);
        Assert.Contains("LinkedIn", snapshot.RecentPlatforms);
    }

    [Fact]
    public async Task InMemoryContentMemoryRepository_RejectsBlankTenantIds()
    {
        var repository = new InMemoryContentMemoryRepository();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetSnapshotAsync("   ", 10, CancellationToken.None));

        Assert.Contains("TenantId is required", exception.Message, StringComparison.Ordinal);
    }
}
