using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Video;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Video;

public sealed class ProcessHeyGenWebhookUseCase
{
    private readonly IVideoGenerationJobRepository _videoGenerationJobRepository;
    private readonly IVideoGenerationJobFinalizer _videoGenerationJobFinalizer;
    private readonly IClock _clock;

    public ProcessHeyGenWebhookUseCase(
        IVideoGenerationJobRepository videoGenerationJobRepository,
        IVideoGenerationJobFinalizer videoGenerationJobFinalizer,
        IClock clock)
    {
        _videoGenerationJobRepository = videoGenerationJobRepository;
        _videoGenerationJobFinalizer = videoGenerationJobFinalizer;
        _clock = clock;
    }

    public async Task<Result<ProcessHeyGenWebhookResponse>> ExecuteAsync(
        string eventType,
        JsonElement eventData,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return Result<ProcessHeyGenWebhookResponse>.Failure("heygen.webhook.event-type.required", "Event type is required.");
        }

        if (!IsSuccessEvent(eventType) && !IsFailureEvent(eventType))
        {
            return Result<ProcessHeyGenWebhookResponse>.Success(
                new ProcessHeyGenWebhookResponse(
                    "Ignored",
                    string.Empty,
                    $"Event type '{eventType}' is not handled by the video webhook processor."));
        }

        if (!TryGetVideoId(eventData, out var providerJobId))
        {
            return Result<ProcessHeyGenWebhookResponse>.Failure("heygen.webhook.video-id.required", "Event payload did not include a video_id.");
        }

        var job = await _videoGenerationJobRepository.FindByProviderJobIdAsync(providerJobId, cancellationToken);
        if (job is null)
        {
            return Result<ProcessHeyGenWebhookResponse>.Success(
                new ProcessHeyGenWebhookResponse(
                    "Ignored",
                    string.Empty,
                    $"No video generation job was found for provider video_id '{providerJobId}'."));
        }

        if (IsSuccessEvent(eventType))
        {
            var finalization = await _videoGenerationJobFinalizer.FinalizeAsync(
                new FinalizeVideoGenerationRequest(job.TenantId.Value, job.VideoGenerationJobId),
                cancellationToken);

            if (finalization.IsFailure)
            {
                return Result<ProcessHeyGenWebhookResponse>.Failure(
                    finalization.ErrorCode ?? "heygen.webhook.finalization.failed",
                    finalization.ErrorMessage ?? "Video finalization failed after webhook receipt.");
            }

            return Result<ProcessHeyGenWebhookResponse>.Success(
                new ProcessHeyGenWebhookResponse(
                    finalization.Value?.Status ?? "Processed",
                    job.VideoGenerationJobId,
                    "Webhook was processed and the video job was finalized."));
        }

        if (IsFailureEvent(eventType))
        {
            var updatedJob = job with
            {
                Status = VideoGenerationJobStatus.Failed,
                FailureReason = ResolveFailureReason(eventData),
                LastCheckedUtc = _clock.UtcNow,
                CompletedUtc = _clock.UtcNow
            };

            await _videoGenerationJobRepository.SaveAsync(updatedJob, cancellationToken);

            return Result<ProcessHeyGenWebhookResponse>.Success(
                new ProcessHeyGenWebhookResponse(
                    "Failed",
                    updatedJob.VideoGenerationJobId,
                    "Webhook marked the video job as failed."));
        }

        return Result<ProcessHeyGenWebhookResponse>.Success(
            new ProcessHeyGenWebhookResponse(
                "Ignored",
                job.VideoGenerationJobId,
                $"Event type '{eventType}' is not handled by the video webhook processor."));
    }

    private static bool TryGetVideoId(JsonElement eventData, out string videoId)
    {
        if (eventData.ValueKind == JsonValueKind.Object &&
            eventData.TryGetProperty("video_id", out var videoIdElement) &&
            videoIdElement.ValueKind == JsonValueKind.String)
        {
            var resolved = videoIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                videoId = resolved.Trim();
                return true;
            }
        }

        videoId = string.Empty;
        return false;
    }

    private static string ResolveFailureReason(JsonElement eventData)
    {
        if (eventData.ValueKind != JsonValueKind.Object)
        {
            return "HeyGen reported that the video job failed.";
        }

        if (eventData.TryGetProperty("message", out var messageElement) &&
            messageElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(messageElement.GetString()))
        {
            return messageElement.GetString()!.Trim();
        }

        if (eventData.TryGetProperty("error", out var errorElement) &&
            errorElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(errorElement.GetString()))
        {
            return errorElement.GetString()!.Trim();
        }

        return "HeyGen reported that the video job failed.";
    }

    private static bool IsSuccessEvent(string eventType) =>
        eventType.Equals("avatar_video.success", StringComparison.OrdinalIgnoreCase) ||
        eventType.Equals("video.success", StringComparison.OrdinalIgnoreCase) ||
        eventType.Equals("video_agent.success", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailureEvent(string eventType) =>
        eventType.Equals("avatar_video.failed", StringComparison.OrdinalIgnoreCase) ||
        eventType.Equals("video.failed", StringComparison.OrdinalIgnoreCase) ||
        eventType.Equals("video_agent.failed", StringComparison.OrdinalIgnoreCase);
}
