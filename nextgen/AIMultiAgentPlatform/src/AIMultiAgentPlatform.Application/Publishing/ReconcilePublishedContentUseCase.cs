using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Publishing;
using AIMultiAgentPlatform.Domain.Publishing;

namespace AIMultiAgentPlatform.Application.Publishing;

public sealed class ReconcilePublishedContentUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISchedulingJobRepository _schedulingJobRepository;
    private readonly IConnectedPublishingProfileRepository _connectedPublishingProfileRepository;
    private readonly IPublishingSecretStore _publishingSecretStore;
    private readonly IPublishedContentRecordRepository _publishedContentRecordRepository;
    private readonly IPublishedContentMetricSnapshotRepository _publishedContentMetricSnapshotRepository;
    private readonly IPublishingProviderSelector _publishingProviderSelector;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public ReconcilePublishedContentUseCase(
        ITenantRepository tenantRepository,
        ISchedulingJobRepository schedulingJobRepository,
        IConnectedPublishingProfileRepository connectedPublishingProfileRepository,
        IPublishingSecretStore publishingSecretStore,
        IPublishedContentRecordRepository publishedContentRecordRepository,
        IPublishedContentMetricSnapshotRepository publishedContentMetricSnapshotRepository,
        IPublishingProviderSelector publishingProviderSelector,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _schedulingJobRepository = schedulingJobRepository;
        _connectedPublishingProfileRepository = connectedPublishingProfileRepository;
        _publishingSecretStore = publishingSecretStore;
        _publishedContentRecordRepository = publishedContentRecordRepository;
        _publishedContentMetricSnapshotRepository = publishedContentMetricSnapshotRepository;
        _publishingProviderSelector = publishingProviderSelector;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<ReconcilePublishedContentResponse>> ExecuteAsync(
        ReconcilePublishedContentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.SchedulingJobId))
        {
            return Result<ReconcilePublishedContentResponse>.Failure(
                "publishing.reconcile.invalid",
                "TenantId and SchedulingJobId are required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<ReconcilePublishedContentResponse>.Failure("publishing.reconcile.tenant.not-found", "Tenant was not found.");
        }

        var schedulingJob = await _schedulingJobRepository.FindByIdAsync(request.SchedulingJobId, cancellationToken);
        if (schedulingJob is null || schedulingJob.TenantId != tenant.TenantId)
        {
            return Result<ReconcilePublishedContentResponse>.Failure("publishing.reconcile.job.not-found", "Scheduling job was not found.");
        }

        var records = await _publishedContentRecordRepository.FindBySchedulingJobIdAsync(schedulingJob.SchedulingJobId, cancellationToken);
        var failures = 0;
        var snapshotsSaved = 0;

        foreach (var record in records)
        {
            var profile = await _connectedPublishingProfileRepository.FindByTenantPlatformAndProviderAsync(
                tenant.TenantId.Value,
                record.Platform,
                record.ProviderName,
                cancellationToken);
            profile ??= await _connectedPublishingProfileRepository.FindByTenantAndPlatformAsync(tenant.TenantId.Value, record.Platform, cancellationToken);
            if (profile is null)
            {
                failures++;
                continue;
            }

            var provider = _publishingProviderSelector.Resolve(record.ProviderName);
            if (provider is null)
            {
                failures++;
                continue;
            }

            var accessToken = await _publishingSecretStore.GetAccessTokenAsync(profile.AccessTokenSecretReference, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                var failedRecord = record with { FailureReason = $"Publishing access token secret '{profile.AccessTokenSecretReference}' could not be resolved." };
                await _publishedContentRecordRepository.SaveAsync(failedRecord, cancellationToken);
                failures++;
                continue;
            }

            var result = await provider.ReconcileAsync(
                new PublishingReconciliationRequest(
                    tenant.TenantId.Value,
                    record.ExternalProfileId,
                    accessToken,
                    record.Platform,
                    record.ExternalPostId,
                    record.ExternalUrl),
                cancellationToken);

            if (!result.Succeeded)
            {
                var failedRecord = record with { FailureReason = result.FailureReason };
                await _publishedContentRecordRepository.SaveAsync(failedRecord, cancellationToken);
                failures++;
                continue;
            }

            var updatedRecord = record with
            {
                ExternalUrl = string.IsNullOrWhiteSpace(result.ExternalUrl) ? record.ExternalUrl : result.ExternalUrl,
                FailureReason = string.Empty,
                PublishedUtc = result.PublishedUtc ?? record.PublishedUtc
            };

            await _publishedContentRecordRepository.SaveAsync(updatedRecord, cancellationToken);
            await _publishedContentMetricSnapshotRepository.SaveAsync(
                new PublishedContentMetricSnapshot(
                    _idGenerator.NewId("published_metric"),
                    record.PublishedContentRecordId,
                    tenant.TenantId,
                    record.ProviderName,
                    record.Platform,
                    result.ProviderStatus,
                    result.Metrics.Reach,
                    result.Metrics.Clicks,
                    result.Metrics.Likes,
                    result.Metrics.Comments,
                    result.Metrics.Shares,
                    _clock.UtcNow),
                cancellationToken);

            snapshotsSaved++;
        }

        return Result<ReconcilePublishedContentResponse>.Success(
            new ReconcilePublishedContentResponse(
                schedulingJob.SchedulingJobId,
                records.Count,
                snapshotsSaved,
                failures));
    }
}
