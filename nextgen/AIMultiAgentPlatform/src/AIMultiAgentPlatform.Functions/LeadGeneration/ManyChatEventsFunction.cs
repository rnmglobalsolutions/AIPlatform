using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.LeadGeneration;
using AIMultiAgentPlatform.Contracts.ManyChat;
using AIMultiAgentPlatform.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIMultiAgentPlatform.Functions.LeadGeneration;

public sealed class ManyChatEventsFunction(ProcessManyChatEventUseCase useCase)
{
    [Function("ProcessManyChatEvent")]
    public async Task<HttpResponseData> PostAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/integrations/manychat/events")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<ProcessManyChatEventRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "ManyChat event could not be processed.", "Request body is required.", cancellationToken);
        }

        var result = await useCase.ExecuteAsync(
            new ProcessManyChatEventCommand(payload, payload.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "ManyChat event could not be processed.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }
}
