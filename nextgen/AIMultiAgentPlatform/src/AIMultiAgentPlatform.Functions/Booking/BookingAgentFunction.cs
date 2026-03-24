using AIMultiAgentPlatform.Application.Booking;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Booking;
using AIMultiAgentPlatform.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIMultiAgentPlatform.Functions.Booking;

public sealed class BookingAgentFunction(OrchestrateBookingAgentUseCase useCase)
{
    [Function("OrchestrateBookingAgent")]
    public async Task<HttpResponseData> OrchestrateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/agents/booking/orchestrate")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<OrchestrateBookingAgentRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Booking orchestration could not be completed.", "Request body is required.", cancellationToken);
        }

        var result = await useCase.ExecuteAsync(
            new OrchestrateBookingAgentCommand(payload, payload.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Booking orchestration could not be completed.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }
}
