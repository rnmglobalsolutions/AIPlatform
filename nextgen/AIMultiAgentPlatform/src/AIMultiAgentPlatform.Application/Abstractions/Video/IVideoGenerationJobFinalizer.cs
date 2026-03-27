using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Video;

namespace AIMultiAgentPlatform.Application.Abstractions.Video;

public interface IVideoGenerationJobFinalizer
{
    Task<Result<FinalizeVideoGenerationResponse>> FinalizeAsync(
        FinalizeVideoGenerationRequest request,
        CancellationToken cancellationToken);
}
