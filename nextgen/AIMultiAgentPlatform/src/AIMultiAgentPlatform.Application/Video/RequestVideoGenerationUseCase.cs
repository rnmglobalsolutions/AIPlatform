using System.Text;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Video;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Video;

public sealed class RequestVideoGenerationUseCase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IDailyContentRequestRepository _dailyContentRequestRepository;
    private readonly IPrimaryAssetRepository _primaryAssetRepository;
    private readonly IVideoGenerationJobRepository _videoGenerationJobRepository;
    private readonly IVideoGenerationProvider _videoGenerationProvider;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public RequestVideoGenerationUseCase(
        ITenantRepository tenantRepository,
        IDailyContentRequestRepository dailyContentRequestRepository,
        IPrimaryAssetRepository primaryAssetRepository,
        IVideoGenerationJobRepository videoGenerationJobRepository,
        IVideoGenerationProvider videoGenerationProvider,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _dailyContentRequestRepository = dailyContentRequestRepository;
        _primaryAssetRepository = primaryAssetRepository;
        _videoGenerationJobRepository = videoGenerationJobRepository;
        _videoGenerationProvider = videoGenerationProvider;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<RequestVideoGenerationResponse>> ExecuteAsync(
        RequestVideoGenerationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<RequestVideoGenerationResponse>.Failure("video.tenant.required", "TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DailyContentRequestId))
        {
            return Result<RequestVideoGenerationResponse>.Failure("video.request.required", "DailyContentRequestId is required.");
        }

        var tenant = await _tenantRepository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<RequestVideoGenerationResponse>.Failure("video.tenant.not-found", "Tenant was not found.");
        }

        var dailyRequest = await _dailyContentRequestRepository.FindByIdAsync(request.DailyContentRequestId, cancellationToken);
        if (dailyRequest is null)
        {
            return Result<RequestVideoGenerationResponse>.Failure("video.request.not-found", "Daily content request was not found.");
        }

        if (dailyRequest.TenantId != tenant.TenantId)
        {
            return Result<RequestVideoGenerationResponse>.Failure("video.request.mismatch", "Daily content request does not belong to the tenant.");
        }

        var primaryAsset = await _primaryAssetRepository.FindByRequestIdAsync(dailyRequest.DailyContentRequestId, cancellationToken);
        if (primaryAsset is null)
        {
            return Result<RequestVideoGenerationResponse>.Failure("video.primary-asset.not-found", "Primary asset was not found.");
        }

        if (primaryAsset.PrimaryFormat != Domain.Editorial.PrimaryFormat.ShortVideo)
        {
            return Result<RequestVideoGenerationResponse>.Failure("video.primary-asset.invalid-format", "Only short-video primary assets can be submitted for video generation.");
        }

        var script = BuildScript(primaryAsset);
        if (string.IsNullOrWhiteSpace(script))
        {
            return Result<RequestVideoGenerationResponse>.Failure(
                "video.primary-asset.empty-script",
                "Primary asset does not contain enough spoken content to generate a video script.");
        }

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? $"video-{tenant.TenantId.Value}-{dailyRequest.DailyContentRequestId}"
            : request.CorrelationId.Trim();
        var providerRequest = new VideoGenerationRequest(
            tenant.TenantId.Value,
            correlationId,
            NormalizeProviderProfile(request.ProviderProfile),
            primaryAsset.Headline,
            script,
            tenant.Profile.ContentLanguage,
            NormalizeAspectRatio(request.AspectRatio),
            new Dictionary<string, string>
            {
                ["dailyContentRequestId"] = dailyRequest.DailyContentRequestId,
                ["primaryAssetId"] = primaryAsset.PrimaryAssetId
            });

        var submissionResult = await _videoGenerationProvider.SubmitAsync(providerRequest, cancellationToken);
        var status = submissionResult.Submitted
            ? VideoGenerationJobStatus.Submitted
            : VideoGenerationJobStatus.Rejected;
        var job = new VideoGenerationJob(
            _idGenerator.NewId("video_job"),
            dailyRequest.DailyContentRequestId,
            tenant.TenantId,
            primaryAsset.PrimaryAssetId,
            submissionResult.ProviderName,
            providerRequest.ProviderProfile,
            submissionResult.ProviderJobId,
            primaryAsset.Headline,
            script,
            providerRequest.Language,
            providerRequest.AspectRatio,
            status,
            submissionResult.FailureReason,
            _clock.UtcNow);

        await _videoGenerationJobRepository.SaveAsync(job, cancellationToken);

        if (!submissionResult.Submitted)
        {
            return Result<RequestVideoGenerationResponse>.Failure("video.submission.rejected", submissionResult.FailureReason);
        }

        return Result<RequestVideoGenerationResponse>.Success(
            new RequestVideoGenerationResponse(
                job.VideoGenerationJobId,
                submissionResult.ProviderName,
                submissionResult.ProviderJobId,
                submissionResult.Status));
    }

    private static string BuildScript(PrimaryAsset primaryAsset)
    {
        var parts = new[]
        {
            primaryAsset.Hook?.Trim() ?? string.Empty,
            primaryAsset.Body?.Trim() ?? string.Empty,
            primaryAsset.Payoff?.Trim() ?? string.Empty,
            primaryAsset.CallToAction?.Trim() ?? string.Empty
        };

        return string.Join("\n\n", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string NormalizeProviderProfile(string? providerProfile) =>
        string.IsNullOrWhiteSpace(providerProfile) ? "default" : providerProfile.Trim();

    private static string NormalizeAspectRatio(string? aspectRatio)
    {
        var normalized = string.IsNullOrWhiteSpace(aspectRatio) ? "9:16" : aspectRatio.Trim();
        return normalized is "9:16" or "1:1" or "16:9" ? normalized : "9:16";
    }
}
