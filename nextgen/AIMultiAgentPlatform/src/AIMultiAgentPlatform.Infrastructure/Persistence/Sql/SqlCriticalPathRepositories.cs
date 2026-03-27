using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Intake;
using AIMultiAgentPlatform.Domain.Publishing;
using AIMultiAgentPlatform.Domain.Strategy;
using AIMultiAgentPlatform.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql;

public sealed class SqlTenantRepository : SqlAggregateDocumentRepositoryBase, ITenantRepository
{
    private const string AggregateType = "Tenant";

    public SqlTenantRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            tenant.TenantId.Value,
            tenant.TenantId.Value,
            tenant,
            tenant.CreatedUtc,
            lookupKey: tenant.Slug,
            sortUtc: tenant.CreatedUtc,
            cancellationToken: cancellationToken);

    public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
        FindByIdAsync<Tenant>(AggregateType, Normalize(tenantId), cancellationToken);
}

public sealed class SqlTallySubmissionReceiptRepository : SqlAggregateDocumentRepositoryBase, ITallySubmissionReceiptRepository
{
    private const string AggregateType = "TallySubmissionReceipt";

    public SqlTallySubmissionReceiptRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(TallySubmissionReceipt receipt, CancellationToken cancellationToken)
    {
        var externalSubmissionId = Normalize(receipt.ExternalSubmissionId);
        return SaveDocumentAsync(
            AggregateType,
            externalSubmissionId,
            receipt.TenantId,
            receipt,
            receipt.ProcessedUtc,
            lookupKey: externalSubmissionId,
            sortUtc: receipt.ProcessedUtc,
            cancellationToken: cancellationToken);
    }

    public Task<TallySubmissionReceipt?> FindByExternalSubmissionIdAsync(string externalSubmissionId, CancellationToken cancellationToken) =>
        FindFirstAsync<TallySubmissionReceipt>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(externalSubmissionId)),
            cancellationToken);
}

public sealed class SqlStrategyPlanRepository : SqlAggregateDocumentRepositoryBase, IStrategyPlanRepository
{
    private const string AggregateType = "StrategyPlan";

    public SqlStrategyPlanRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(StrategyPlan strategyPlan, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            strategyPlan.StrategyPlanId,
            strategyPlan.TenantId.Value,
            strategyPlan,
            strategyPlan.CreatedUtc,
            sortUtc: strategyPlan.CreatedUtc,
            cancellationToken: cancellationToken);
}

public sealed class SqlEditorialBacklogRepository : SqlAggregateDocumentRepositoryBase, IEditorialBacklogRepository
{
    private const string AggregateType = "EditorialBacklog";

    public SqlEditorialBacklogRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(EditorialBacklog backlog, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            backlog.EditorialBacklogId,
            backlog.TenantId.Value,
            backlog,
            backlog.SeededUtc,
            sortUtc: backlog.SeededUtc,
            cancellationToken: cancellationToken);

    public Task<EditorialBacklog?> FindByIdAsync(string backlogId, CancellationToken cancellationToken) =>
        FindByIdAsync<EditorialBacklog>(AggregateType, Normalize(backlogId), cancellationToken);
}

public sealed class SqlDailyContentRequestRepository : SqlAggregateDocumentRepositoryBase, IDailyContentRequestRepository
{
    private const string AggregateType = "DailyContentRequest";

    public SqlDailyContentRequestRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(DailyContentRequest request, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            request.DailyContentRequestId,
            request.TenantId.Value,
            request,
            request.RequestedUtc,
            lookupKey: request.EditorialBacklogId,
            lookupKey2: request.EditorialBacklogSequence.ToString(),
            sortUtc: request.RequestedUtc,
            cancellationToken: cancellationToken);

    public Task<DailyContentRequest?> FindByIdAsync(string requestId, CancellationToken cancellationToken) =>
        FindByIdAsync<DailyContentRequest>(AggregateType, Normalize(requestId), cancellationToken);
}

public sealed class SqlDailyContentBriefRepository : SqlAggregateDocumentRepositoryBase, IDailyContentBriefRepository
{
    private const string AggregateType = "DailyContentBrief";

    public SqlDailyContentBriefRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(DailyContentBrief brief, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            brief.DailyContentBriefId,
            brief.TenantId.Value,
            brief,
            DateTime.UtcNow,
            lookupKey: brief.DailyContentRequestId,
            sortUtc: DateTime.UtcNow,
            cancellationToken: cancellationToken);

    public Task<DailyContentBrief?> FindByIdAsync(string briefId, CancellationToken cancellationToken) =>
        FindByIdAsync<DailyContentBrief>(AggregateType, Normalize(briefId), cancellationToken);

    public Task<DailyContentBrief?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        FindFirstAsync<DailyContentBrief>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(requestId)),
            cancellationToken);
}

public sealed class SqlPrimaryAssetRepository : SqlAggregateDocumentRepositoryBase, IPrimaryAssetRepository
{
    private const string AggregateType = "PrimaryAsset";

    public SqlPrimaryAssetRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(PrimaryAsset asset, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            asset.PrimaryAssetId,
            asset.TenantId.Value,
            asset,
            DateTime.UtcNow,
            lookupKey: asset.DailyContentRequestId,
            lookupKey2: asset.PrimaryFormat.ToString(),
            sortUtc: DateTime.UtcNow,
            cancellationToken: cancellationToken);

    public Task<PrimaryAsset?> FindByIdAsync(string primaryAssetId, CancellationToken cancellationToken) =>
        FindByIdAsync<PrimaryAsset>(AggregateType, Normalize(primaryAssetId), cancellationToken);

    public Task<PrimaryAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        FindFirstAsync<PrimaryAsset>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(requestId)),
            cancellationToken);
}

public sealed class SqlCaptionAssetRepository : SqlAggregateDocumentRepositoryBase, ICaptionAssetRepository
{
    private const string AggregateType = "CaptionAsset";

    public SqlCaptionAssetRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(CaptionAsset asset, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            asset.CaptionAssetId,
            tenantId: string.Empty,
            asset,
            DateTime.UtcNow,
            lookupKey: asset.DailyContentRequestId,
            sortUtc: DateTime.UtcNow,
            cancellationToken: cancellationToken);

    public Task<CaptionAsset?> FindByIdAsync(string captionAssetId, CancellationToken cancellationToken) =>
        FindByIdAsync<CaptionAsset>(AggregateType, Normalize(captionAssetId), cancellationToken);

    public Task<CaptionAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        FindFirstAsync<CaptionAsset>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(requestId)),
            cancellationToken);
}

public sealed class SqlRepurposedAssetBundleRepository : SqlAggregateDocumentRepositoryBase, IRepurposedAssetBundleRepository
{
    private const string AggregateType = "RepurposedAssetBundle";

    public SqlRepurposedAssetBundleRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(RepurposedAssetBundle bundle, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            bundle.RepurposedAssetBundleId,
            tenantId: string.Empty,
            bundle,
            DateTime.UtcNow,
            lookupKey: bundle.DailyContentRequestId,
            sortUtc: DateTime.UtcNow,
            cancellationToken: cancellationToken);

    public Task<RepurposedAssetBundle?> FindByIdAsync(string bundleId, CancellationToken cancellationToken) =>
        FindByIdAsync<RepurposedAssetBundle>(AggregateType, Normalize(bundleId), cancellationToken);

    public Task<RepurposedAssetBundle?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        FindFirstAsync<RepurposedAssetBundle>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(requestId)),
            cancellationToken);
}

public sealed class SqlVideoGenerationJobRepository : SqlAggregateDocumentRepositoryBase, IVideoGenerationJobRepository
{
    private const string AggregateType = "VideoGenerationJob";

    public SqlVideoGenerationJobRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(VideoGenerationJob job, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            job.VideoGenerationJobId,
            job.TenantId.Value,
            job,
            job.RequestedUtc,
            lookupKey: job.DailyContentRequestId,
            lookupKey2: job.ProviderJobId,
            lookupKey3: job.Status.ToString(),
            sortUtc: job.RequestedUtc,
            cancellationToken: cancellationToken);

    public Task<VideoGenerationJob?> FindByIdAsync(string videoGenerationJobId, CancellationToken cancellationToken) =>
        FindByIdAsync<VideoGenerationJob>(AggregateType, Normalize(videoGenerationJobId), cancellationToken);

    public Task<VideoGenerationJob?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
        FindFirstAsync<VideoGenerationJob>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(dailyContentRequestId)),
            cancellationToken);

    public Task<VideoGenerationJob?> FindByProviderJobIdAsync(string providerJobId, CancellationToken cancellationToken) =>
        FindFirstAsync<VideoGenerationJob>(
            AggregateType,
            query => query.Where(item => item.LookupKey2 == Normalize(providerJobId)),
            cancellationToken);

    public Task<IReadOnlyList<VideoGenerationJob>> ListActiveAsync(int maxCount, CancellationToken cancellationToken)
    {
        var take = maxCount <= 0 ? 10 : maxCount;
        return ListAsync<VideoGenerationJob>(
            AggregateType,
            query => query
                .Where(item =>
                    item.LookupKey3 == VideoGenerationJobStatus.Submitted.ToString() ||
                    item.LookupKey3 == VideoGenerationJobStatus.Processing.ToString())
                .OrderBy(item => item.SortUtc)
                .Take(take),
            cancellationToken);
    }
}

public sealed class SqlGeneratedVideoAssetRepository : SqlAggregateDocumentRepositoryBase, IGeneratedVideoAssetRepository
{
    private const string AggregateType = "GeneratedVideoAsset";

    public SqlGeneratedVideoAssetRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(GeneratedVideoAsset asset, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            asset.GeneratedVideoAssetId,
            asset.TenantId.Value,
            asset,
            asset.CreatedUtc,
            lookupKey: asset.VideoGenerationJobId,
            lookupKey2: asset.DailyContentRequestId,
            sortUtc: asset.CreatedUtc,
            cancellationToken: cancellationToken);

    public Task<GeneratedVideoAsset?> FindByJobIdAsync(string videoGenerationJobId, CancellationToken cancellationToken) =>
        FindFirstAsync<GeneratedVideoAsset>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(videoGenerationJobId)),
            cancellationToken);

    public Task<GeneratedVideoAsset?> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
        FindFirstAsync<GeneratedVideoAsset>(
            AggregateType,
            query => query.Where(item => item.LookupKey2 == Normalize(dailyContentRequestId)),
            cancellationToken);
}

public sealed class SqlSchedulingJobRepository : SqlAggregateDocumentRepositoryBase, ISchedulingJobRepository
{
    private const string AggregateType = "SchedulingJob";

    public SqlSchedulingJobRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(SchedulingJob job, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            job.SchedulingJobId,
            job.TenantId.Value,
            job,
            job.CreatedUtc,
            lookupKey: job.DailyContentRequestId,
            lookupKey2: job.Status.ToString(),
            sortUtc: job.CreatedUtc,
            cancellationToken: cancellationToken);

    public Task<SchedulingJob?> FindByIdAsync(string schedulingJobId, CancellationToken cancellationToken) =>
        FindByIdAsync<SchedulingJob>(AggregateType, Normalize(schedulingJobId), cancellationToken);

    public Task<SchedulingJob?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        FindFirstAsync<SchedulingJob>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(requestId)),
            cancellationToken);
}

public sealed class SqlConnectedPublishingProfileRepository : SqlAggregateDocumentRepositoryBase, IConnectedPublishingProfileRepository
{
    private const string AggregateType = "ConnectedPublishingProfile";

    public SqlConnectedPublishingProfileRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(ConnectedPublishingProfile profile, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            profile.ConnectedPublishingProfileId,
            profile.TenantId.Value,
            profile,
            profile.CreatedUtc,
            lookupKey: Normalize(profile.Platform).ToLowerInvariant(),
            lookupKey2: Normalize(profile.ProviderName),
            sortUtc: profile.UpdatedUtc,
            cancellationToken: cancellationToken);

    public Task<ConnectedPublishingProfile?> FindByTenantAndPlatformAsync(string tenantId, string platform, CancellationToken cancellationToken) =>
        FindFirstAsync<ConnectedPublishingProfile>(
            AggregateType,
            query => query.Where(item =>
                item.TenantId == Normalize(tenantId) &&
                item.LookupKey == Normalize(platform).ToLowerInvariant())
                .OrderByDescending(item => item.SortUtc)
                .ThenBy(item => item.LookupKey2),
            cancellationToken);

    public Task<ConnectedPublishingProfile?> FindByTenantPlatformAndProviderAsync(string tenantId, string platform, string providerName, CancellationToken cancellationToken) =>
        FindFirstAsync<ConnectedPublishingProfile>(
            AggregateType,
            query => query.Where(item =>
                item.TenantId == Normalize(tenantId) &&
                item.LookupKey == Normalize(platform).ToLowerInvariant() &&
                item.LookupKey2 == Normalize(providerName))
                .OrderByDescending(item => item.SortUtc),
            cancellationToken);

    public Task<IReadOnlyList<ConnectedPublishingProfile>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken) =>
        ListAsync<ConnectedPublishingProfile>(
            AggregateType,
            query => query
                .Where(item => item.TenantId == Normalize(tenantId))
                .OrderByDescending(item => item.SortUtc)
                .ThenBy(item => item.LookupKey)
                .ThenBy(item => item.LookupKey2),
            cancellationToken);
}

public sealed class SqlPublishedContentRecordRepository : SqlAggregateDocumentRepositoryBase, IPublishedContentRecordRepository
{
    private const string AggregateType = "PublishedContentRecord";

    public SqlPublishedContentRecordRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(PublishedContentRecord record, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            record.PublishedContentRecordId,
            record.TenantId.Value,
            record,
            record.PublishedUtc,
            lookupKey: record.DailyContentRequestId,
            lookupKey2: record.SchedulingJobId,
            lookupKey3: record.Status.ToString(),
            sortUtc: record.PublishedUtc,
            cancellationToken: cancellationToken);

    public Task<PublishedContentRecord?> FindByIdAsync(string publishedContentRecordId, CancellationToken cancellationToken) =>
        FindByIdAsync<PublishedContentRecord>(AggregateType, Normalize(publishedContentRecordId), cancellationToken);

    public Task<IReadOnlyList<PublishedContentRecord>> FindByRequestIdAsync(string dailyContentRequestId, CancellationToken cancellationToken) =>
        ListAsync<PublishedContentRecord>(
            AggregateType,
            query => query
                .Where(item => item.LookupKey == Normalize(dailyContentRequestId))
                .OrderBy(item => item.SortUtc),
            cancellationToken);

    public Task<IReadOnlyList<PublishedContentRecord>> FindBySchedulingJobIdAsync(string schedulingJobId, CancellationToken cancellationToken) =>
        ListAsync<PublishedContentRecord>(
            AggregateType,
            query => query
                .Where(item => item.LookupKey2 == Normalize(schedulingJobId))
                .OrderBy(item => item.SortUtc),
            cancellationToken);
}

public sealed class SqlPublishedContentMetricSnapshotRepository : SqlAggregateDocumentRepositoryBase, IPublishedContentMetricSnapshotRepository
{
    private const string AggregateType = "PublishedContentMetricSnapshot";

    public SqlPublishedContentMetricSnapshotRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(PublishedContentMetricSnapshot snapshot, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            snapshot.PublishedContentMetricSnapshotId,
            snapshot.TenantId.Value,
            snapshot,
            snapshot.CapturedUtc,
            lookupKey: snapshot.PublishedContentRecordId,
            lookupKey2: snapshot.ProviderStatus,
            sortUtc: snapshot.CapturedUtc,
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<PublishedContentMetricSnapshot>> FindByPublishedContentRecordIdAsync(string publishedContentRecordId, CancellationToken cancellationToken) =>
        ListAsync<PublishedContentMetricSnapshot>(
            AggregateType,
            query => query
                .Where(item => item.LookupKey == Normalize(publishedContentRecordId))
                .OrderBy(item => item.SortUtc),
            cancellationToken);
}

public sealed class SqlVideoWebhookEndpointRegistrationRepository : SqlAggregateDocumentRepositoryBase, IVideoWebhookEndpointRegistrationRepository
{
    private const string AggregateType = "VideoWebhookEndpointRegistration";

    public SqlVideoWebhookEndpointRegistrationRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(VideoWebhookEndpointRegistration registration, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            Normalize(registration.ProviderName),
            tenantId: string.Empty,
            registration,
            registration.CreatedUtc,
            lookupKey: Normalize(registration.ProviderName),
            sortUtc: registration.LastSyncedUtc,
            cancellationToken: cancellationToken);

    public Task<VideoWebhookEndpointRegistration?> FindByProviderAsync(string providerName, CancellationToken cancellationToken) =>
        FindFirstAsync<VideoWebhookEndpointRegistration>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(providerName)),
            cancellationToken);

    public Task DeleteAsync(string providerName, CancellationToken cancellationToken) =>
        DeleteDocumentsAsync(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(providerName)),
            cancellationToken);
}

public sealed class SqlContentMemoryRepository : SqlAggregateDocumentRepositoryBase, IContentMemoryRepository
{
    private const string AggregateType = "ContentMemoryEntry";

    public SqlContentMemoryRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(ContentMemoryEntry entry, CancellationToken cancellationToken)
    {
        var effectiveUtc = entry.PublishedUtc ?? entry.CreatedUtc;
        return SaveDocumentAsync(
            AggregateType,
            entry.ContentMemoryEntryId,
            entry.TenantId.Value,
            entry,
            entry.CreatedUtc,
            lookupKey: entry.ContentHash,
            lookupKey2: entry.LifecycleStage.ToString(),
            lookupKey3: entry.Platform,
            sortUtc: effectiveUtc,
            cancellationToken: cancellationToken);
    }

    public async Task<ContentMemorySnapshot> GetSnapshotAsync(string tenantId, int maxEntries, CancellationToken cancellationToken)
    {
        var normalizedTenantId = Normalize(tenantId);
        if (string.IsNullOrWhiteSpace(normalizedTenantId))
        {
            throw new ArgumentException("TenantId is required when saving or loading content memory.", nameof(tenantId));
        }

        var normalizedMaxEntries = Math.Clamp(maxEntries, 1, 100);
        var entries = await ListAsync<ContentMemoryEntry>(
            AggregateType,
            query => query
                .Where(item => item.TenantId == normalizedTenantId)
                .OrderByDescending(item => item.SortUtc)
                .ThenByDescending(item => item.UpdatedUtc)
                .Take(normalizedMaxEntries),
            cancellationToken);

        if (entries.Count == 0)
        {
            return ContentMemorySnapshot.Empty(new TenantId(normalizedTenantId), DateTime.UtcNow);
        }

        return new ContentMemorySnapshot(
            new TenantId(normalizedTenantId),
            DateTime.UtcNow,
            entries,
            DistinctNonEmpty(entries.Select(static entry => entry.Topic)),
            DistinctNonEmpty(entries.Select(static entry => entry.PrimaryHook)),
            DistinctNonEmpty(entries.Select(static entry => entry.CallToActionPattern)),
            DistinctNonEmpty(entries.Select(static entry => entry.Platform)),
            DistinctNonEmpty(entries.Select(static entry => entry.LeadGoal)),
            DistinctNonEmpty(entries.Select(static entry => entry.ContentHash)));
    }

    private static IReadOnlyList<string> DistinctNonEmpty(IEnumerable<string> values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
