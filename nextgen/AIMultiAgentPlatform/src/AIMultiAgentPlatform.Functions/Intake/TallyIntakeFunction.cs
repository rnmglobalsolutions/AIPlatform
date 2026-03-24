using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIMultiAgentPlatform.Functions.Intake;

public sealed class TallyIntakeFunction(ProcessTallySubmissionUseCase useCase)
{
    [Function("TallyIntakePost")]
    public async Task<HttpResponseData> PostAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/intake/tally")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<TallySubmissionRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be processed.", "Request body is required.", cancellationToken);
        }

        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(payload), cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Tally submission could not be processed.", Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."), cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }
}
