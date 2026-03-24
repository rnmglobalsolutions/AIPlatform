using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Voice;
using AIMultiAgentPlatform.Contracts.Voice;
using AIMultiAgentPlatform.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIMultiAgentPlatform.Functions.Voice;

public sealed class VoiceAgentFunction(OrchestrateVoiceAgentUseCase useCase)
{
    [Function("OrchestrateVoiceAgent")]
    public async Task<HttpResponseData> OrchestrateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/agents/voice/orchestrate")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<OrchestrateVoiceAgentRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Voice orchestration could not be completed.", "Request body is required.", cancellationToken);
        }

        var result = await useCase.ExecuteAsync(
            new OrchestrateVoiceAgentCommand(payload, payload.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Voice orchestration could not be completed.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }
}
