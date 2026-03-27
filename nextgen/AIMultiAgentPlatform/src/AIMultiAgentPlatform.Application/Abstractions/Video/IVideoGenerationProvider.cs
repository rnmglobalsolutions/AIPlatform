namespace AIMultiAgentPlatform.Application.Abstractions.Video;

public interface IVideoGenerationProvider
{
    string ProviderName { get; }

    Task<VideoGenerationSubmissionResult> SubmitAsync(VideoGenerationRequest request, CancellationToken cancellationToken);

    Task<VideoGenerationStatusResult> GetStatusAsync(string providerJobId, CancellationToken cancellationToken);
}
