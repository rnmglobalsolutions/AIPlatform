using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Content;
using AIMultiAgentPlatform.Contracts.Video;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Video;

public sealed class FinalizeVideoGenerationUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IVideoGenerationJobRepository _videoGenerationJobRepository;
    private readonly IGeneratedVideoAssetRepository _generatedVideoAssetRepository;
    private readonly IVideoGenerationProvider _videoGenerationProvider;
    private readonly IVideoAssetStore _videoAssetStore;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly RefreshRepurposedAssetBundleFromVideoUseCase? _refreshRepurposedAssetBundleFromVideoUseCase;

    public FinalizeVideoGenerationUseCase(
        ITenantRepository tenantRepository,
        IVideoGenerationJobRepository videoGenerationJobRepository,
        IGeneratedVideoAssetRepository generatedVideoAssetRepository,
        IVideoGenerationProvider videoGenerationProvider,
        IVideoAssetStore videoAssetStore,
        IIdGenerator idGenerator,
        IClock clock,
        RefreshRepurposedAssetBundleFromVideoUseCase? refreshRepurposedAssetBundleFromVideoUseCase = null)
    {
        _tenantRepository = tenantRepository;
        _videoGenerationJobRepository = videoGenerationJobRepository;
        _generatedVideoAssetRepository = generatedVideoAssetRepository;
        _videoGenerationProvider = videoGenerationProvider;
        _videoAssetStore = videoAssetStore;
        _idGenerator = idGenerator;
        _clock = clock;
        _refreshRepurposedAssetBundleFromVideoUseCase = refreshRepurposedAssetBundleFromVideoUseCase;
    }

    public async Task<Result<FinalizeVideoGenerationResponse>> ExecuteAsync(
        FinalizeVideoGenerationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<FinalizeVideoGenerationResponse>.Failure("video.tenant.required", "TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.VideoGenerationJobId))
        {
            return Result<FinalizeVideoGenerationResponse>.Failure("video.job.required", "VideoGenerationJobId is required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<FinalizeVideoGenerationResponse>.Failure("video.tenant.not-found", "Tenant was not found.");
        }

        var job = await _videoGenerationJobRepository.FindByIdAsync(request.VideoGenerationJobId, cancellationToken);
        if (job is null)
        {
            return Result<FinalizeVideoGenerationResponse>.Failure("video.job.not-found", "Video generation job was not found.");
        }

        if (job.TenantId != tenant.TenantId)
        {
            return Result<FinalizeVideoGenerationResponse>.Failure("video.job.mismatch", "Video generation job does not belong to the tenant.");
        }

        if (job.Status == VideoGenerationJobStatus.Rejected)
        {
            return Result<FinalizeVideoGenerationResponse>.Failure("video.job.rejected", job.FailureReason);
        }

        var existingAsset = await _generatedVideoAssetRepository.FindByJobIdAsync(job.VideoGenerationJobId, cancellationToken);
        if (job.Status == VideoGenerationJobStatus.Completed && existingAsset is not null)
        {
            return Result<FinalizeVideoGenerationResponse>.Success(
                new FinalizeVideoGenerationResponse(
                    job.VideoGenerationJobId,
                    job.Status.ToString(),
                    existingAsset.GeneratedVideoAssetId,
                    existingAsset.VideoUrl));
        }

        var providerStatus = await _videoGenerationProvider.GetStatusAsync(job.ProviderJobId, cancellationToken);
        var mappedStatus = MapStatus(providerStatus.Status);
        var updatedJob = job with
        {
            Status = mappedStatus,
            FailureReason = providerStatus.FailureReason,
            LastCheckedUtc = _clock.UtcNow,
            CompletedUtc = mappedStatus is VideoGenerationJobStatus.Completed or VideoGenerationJobStatus.Failed
                ? _clock.UtcNow
                : job.CompletedUtc
        };

        await _videoGenerationJobRepository.SaveAsync(updatedJob, cancellationToken);

        if (mappedStatus == VideoGenerationJobStatus.Failed)
        {
            return Result<FinalizeVideoGenerationResponse>.Failure(
                "video.job.failed",
                string.IsNullOrWhiteSpace(updatedJob.FailureReason)
                    ? "Video generation provider reported a failed job."
                    : updatedJob.FailureReason);
        }

        if (mappedStatus != VideoGenerationJobStatus.Completed)
        {
            return Result<FinalizeVideoGenerationResponse>.Success(
                new FinalizeVideoGenerationResponse(
                    updatedJob.VideoGenerationJobId,
                    mappedStatus.ToString(),
                    string.Empty,
                    string.Empty));
        }

        existingAsset = await _generatedVideoAssetRepository.FindByJobIdAsync(updatedJob.VideoGenerationJobId, cancellationToken);
        if (existingAsset is not null)
        {
            return Result<FinalizeVideoGenerationResponse>.Success(
                new FinalizeVideoGenerationResponse(
                    updatedJob.VideoGenerationJobId,
                    mappedStatus.ToString(),
                    existingAsset.GeneratedVideoAssetId,
                    existingAsset.VideoUrl));
        }

        if (string.IsNullOrWhiteSpace(providerStatus.VideoDownloadUrl))
        {
            return Result<FinalizeVideoGenerationResponse>.Failure(
                "video.job.missing-download-url",
                "Video generation completed, but the provider did not return a downloadable video URL.");
        }

        var storageResult = await _videoAssetStore.StoreAsync(
            new VideoAssetStorageRequest(
                updatedJob.TenantId.Value,
                updatedJob.DailyContentRequestId,
                updatedJob.VideoGenerationJobId,
                updatedJob.ProviderName,
                providerStatus.VideoDownloadUrl,
                $"{updatedJob.VideoGenerationJobId}.mp4"),
            cancellationToken);

        if (!storageResult.Succeeded)
        {
            return Result<FinalizeVideoGenerationResponse>.Failure(
                "video.asset-store.failed",
                storageResult.FailureReason);
        }

        var generatedAsset = new GeneratedVideoAsset(
            _idGenerator.NewId("video_asset"),
            updatedJob.VideoGenerationJobId,
            updatedJob.DailyContentRequestId,
            updatedJob.TenantId,
            updatedJob.PrimaryAssetId,
            string.IsNullOrWhiteSpace(providerStatus.ProviderName) ? updatedJob.ProviderName : providerStatus.ProviderName,
            providerStatus.ProviderJobId,
            updatedJob.Title,
            providerStatus.VideoDownloadUrl,
            storageResult.VideoUrl,
            providerStatus.TranscriptText,
            updatedJob.Language,
            updatedJob.AspectRatio,
            _clock.UtcNow,
            providerStatus.TranscriptSegments);

        await _generatedVideoAssetRepository.SaveAsync(generatedAsset, cancellationToken);
        if (_refreshRepurposedAssetBundleFromVideoUseCase is not null)
        {
            await _refreshRepurposedAssetBundleFromVideoUseCase.ExecuteAsync(
                new Contracts.Content.RefreshRepurposedAssetBundleFromVideoRequest(updatedJob.DailyContentRequestId),
                cancellationToken);
        }

        return Result<FinalizeVideoGenerationResponse>.Success(
            new FinalizeVideoGenerationResponse(
                updatedJob.VideoGenerationJobId,
                mappedStatus.ToString(),
                generatedAsset.GeneratedVideoAssetId,
                generatedAsset.VideoUrl));
    }

    private static VideoGenerationJobStatus MapStatus(string? providerStatus)
    {
        if (string.IsNullOrWhiteSpace(providerStatus))
        {
            return VideoGenerationJobStatus.Processing;
        }

        return providerStatus.Trim().ToLowerInvariant() switch
        {
            "completed" or "complete" or "succeeded" => VideoGenerationJobStatus.Completed,
            "failed" or "error" or "rejected" => VideoGenerationJobStatus.Failed,
            "submitted" or "queued" => VideoGenerationJobStatus.Submitted,
            _ => VideoGenerationJobStatus.Processing
        };
    }
}
