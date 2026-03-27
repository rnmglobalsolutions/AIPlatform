using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Video;

namespace AIMultiAgentPlatform.Application.Video;

public sealed class VideoGenerationJobFinalizer : IVideoGenerationJobFinalizer
{
    private readonly FinalizeVideoGenerationUseCase _finalizeVideoGenerationUseCase;

    public VideoGenerationJobFinalizer(FinalizeVideoGenerationUseCase finalizeVideoGenerationUseCase) =>
        _finalizeVideoGenerationUseCase = finalizeVideoGenerationUseCase;

    public Task<Result<FinalizeVideoGenerationResponse>> FinalizeAsync(
        FinalizeVideoGenerationRequest request,
        CancellationToken cancellationToken) =>
        _finalizeVideoGenerationUseCase.ExecuteAsync(request, cancellationToken);
}
