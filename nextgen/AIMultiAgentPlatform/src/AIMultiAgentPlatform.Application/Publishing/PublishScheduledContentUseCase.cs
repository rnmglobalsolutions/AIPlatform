using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Publishing;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Application.Publishing;

public sealed class PublishScheduledContentUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISchedulingJobRepository _schedulingJobRepository;
    private readonly IPrimaryAssetRepository _primaryAssetRepository;
    private readonly ICaptionAssetRepository _captionAssetRepository;
    private readonly IGeneratedVideoAssetRepository _generatedVideoAssetRepository;
    private readonly IConnectedPublishingProfileRepository _connectedPublishingProfileRepository;
    private readonly IPublishedContentRecordRepository _publishedContentRecordRepository;
    private readonly IPublishingProviderSelector _publishingProviderSelector;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public PublishScheduledContentUseCase(
        ITenantRepository tenantRepository,
        ISchedulingJobRepository schedulingJobRepository,
        IPrimaryAssetRepository primaryAssetRepository,
        ICaptionAssetRepository captionAssetRepository,
        IGeneratedVideoAssetRepository generatedVideoAssetRepository,
        IConnectedPublishingProfileRepository connectedPublishingProfileRepository,
        IPublishedContentRecordRepository publishedContentRecordRepository,
        IPublishingProviderSelector publishingProviderSelector,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _schedulingJobRepository = schedulingJobRepository;
        _primaryAssetRepository = primaryAssetRepository;
        _captionAssetRepository = captionAssetRepository;
        _generatedVideoAssetRepository = generatedVideoAssetRepository;
        _connectedPublishingProfileRepository = connectedPublishingProfileRepository;
        _publishedContentRecordRepository = publishedContentRecordRepository;
        _publishingProviderSelector = publishingProviderSelector;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<PublishScheduledContentResponse>> ExecuteAsync(
        PublishScheduledContentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.SchedulingJobId))
        {
            return Result<PublishScheduledContentResponse>.Failure(
                "publishing.job.invalid",
                "TenantId and SchedulingJobId are required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<PublishScheduledContentResponse>.Failure("publishing.tenant.not-found", "Tenant was not found.");
        }

        var schedulingJob = await _schedulingJobRepository.FindByIdAsync(request.SchedulingJobId, cancellationToken);
        if (schedulingJob is null || schedulingJob.TenantId != tenant.TenantId)
        {
            return Result<PublishScheduledContentResponse>.Failure("publishing.job.not-found", "Scheduling job was not found.");
        }

        if (schedulingJob.Status == SchedulingStatus.Blocked)
        {
            return Result<PublishScheduledContentResponse>.Failure("publishing.job.blocked", "Scheduling job is blocked.");
        }

        var primaryAsset = await _primaryAssetRepository.FindByRequestIdAsync(schedulingJob.DailyContentRequestId, cancellationToken);
        var captionAsset = await _captionAssetRepository.FindByRequestIdAsync(schedulingJob.DailyContentRequestId, cancellationToken);
        if (primaryAsset is null || captionAsset is null)
        {
            return Result<PublishScheduledContentResponse>.Failure("publishing.package.incomplete", "Primary asset or caption asset was not found.");
        }

        var generatedVideoAsset = await _generatedVideoAssetRepository.FindByRequestIdAsync(schedulingJob.DailyContentRequestId, cancellationToken);
        if (primaryAsset.PrimaryFormat == Domain.Editorial.PrimaryFormat.ShortVideo && generatedVideoAsset is null)
        {
            return Result<PublishScheduledContentResponse>.Failure(
                "publishing.video-asset.required",
                "Short video content cannot be published until the generated video asset is available.");
        }

        var publishedCount = 0;
        var failedCount = 0;

        foreach (var target in schedulingJob.Targets)
        {
            var connectedProfile = await _connectedPublishingProfileRepository.FindByTenantAndPlatformAsync(tenant.TenantId.Value, target.Platform, cancellationToken);
            if (connectedProfile is null)
            {
                await SaveRecordAsync(tenant.TenantId, schedulingJob, target, primaryAsset, captionAsset, null, ResolveAssetUrl(primaryAsset, generatedVideoAsset), PublishedContentStatus.Failed, string.Empty, string.Empty, "No connected publishing profile was found for the target platform.", cancellationToken);
                failedCount++;
                continue;
            }

            var provider = _publishingProviderSelector.Resolve(connectedProfile.ProviderName);
            if (provider is null)
            {
                await SaveRecordAsync(tenant.TenantId, schedulingJob, target, primaryAsset, captionAsset, connectedProfile, ResolveAssetUrl(primaryAsset, generatedVideoAsset), PublishedContentStatus.Failed, string.Empty, string.Empty, $"Publishing provider '{connectedProfile.ProviderName}' is not registered.", cancellationToken);
                failedCount++;
                continue;
            }

            var assetUrl = ResolveAssetUrl(primaryAsset, generatedVideoAsset);
            var result = await provider.PublishAsync(
                new PublishingRequest(
                    tenant.TenantId.Value,
                    schedulingJob.SchedulingJobId,
                    connectedProfile.ExternalProfileId,
                    connectedProfile.AccessToken,
                    target.Platform,
                    captionAsset.Caption,
                    assetUrl,
                    target.ScheduledUtc,
                    new Dictionary<string, string>
                    {
                        ["providerName"] = connectedProfile.ProviderName,
                        ["primaryFormat"] = primaryAsset.PrimaryFormat.ToString()
                    }),
                cancellationToken);

            if (result.Succeeded)
            {
                await SaveRecordAsync(tenant.TenantId, schedulingJob, target, primaryAsset, captionAsset, connectedProfile, assetUrl, PublishedContentStatus.Published, result.ExternalPostId, result.ExternalUrl, string.Empty, cancellationToken);
                publishedCount++;
            }
            else
            {
                await SaveRecordAsync(tenant.TenantId, schedulingJob, target, primaryAsset, captionAsset, connectedProfile, assetUrl, PublishedContentStatus.Failed, string.Empty, string.Empty, result.FailureReason, cancellationToken);
                failedCount++;
            }
        }

        var updatedJob = schedulingJob with
        {
            Status = publishedCount > 0 && failedCount == 0
                ? SchedulingStatus.Published
                : publishedCount > 0
                    ? SchedulingStatus.PartiallyPublished
                    : SchedulingStatus.Failed,
            DecisionReason = publishedCount > 0 && failedCount == 0
                ? "Content published successfully on all scheduled targets."
                : publishedCount > 0
                    ? "Content published on some targets, but at least one target failed."
                    : "Publishing failed on all scheduled targets."
        };

        await _schedulingJobRepository.SaveAsync(updatedJob, cancellationToken);

        return Result<PublishScheduledContentResponse>.Success(
            new PublishScheduledContentResponse(
                updatedJob.SchedulingJobId,
                updatedJob.Status.ToString(),
                publishedCount,
                failedCount));
    }

    private async Task SaveRecordAsync(
        Domain.Common.TenantId tenantId,
        SchedulingJob schedulingJob,
        PublicationTarget target,
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        ConnectedPublishingProfile? connectedProfile,
        string assetUrl,
        PublishedContentStatus status,
        string externalPostId,
        string externalUrl,
        string failureReason,
        CancellationToken cancellationToken)
    {
        await _publishedContentRecordRepository.SaveAsync(
            new PublishedContentRecord(
                _idGenerator.NewId("published"),
                schedulingJob.DailyContentRequestId,
                schedulingJob.SchedulingJobId,
                tenantId,
                connectedProfile?.ProviderName ?? "Unknown",
                target.Platform,
                connectedProfile?.ExternalProfileId ?? string.Empty,
                externalPostId,
                externalUrl,
                captionAsset.Caption,
                assetUrl,
                status,
                failureReason,
                _clock.UtcNow),
            cancellationToken);
    }

    private static string ResolveAssetUrl(PrimaryAsset primaryAsset, GeneratedVideoAsset? generatedVideoAsset)
    {
        if (primaryAsset.PrimaryFormat == Domain.Editorial.PrimaryFormat.ShortVideo && generatedVideoAsset is not null)
        {
            return generatedVideoAsset.VideoUrl;
        }

        return string.Empty;
    }
}
