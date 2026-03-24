using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Contracts.Reporting;
using AIMultiAgentPlatform.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIMultiAgentPlatform.Functions.Reporting;

public sealed class ReportingFunction(GenerateMonthlyPerformanceSnapshotUseCase useCase)
{
    [Function("GenerateMonthlyPerformanceSnapshot")]
    public async Task<HttpResponseData> GenerateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/reporting/monthly-snapshots/generate")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<GenerateMonthlyPerformanceSnapshotRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Monthly performance snapshot could not be generated.", "Request body is required.", cancellationToken);
        }

        var result = await useCase.ExecuteAsync(
            new GenerateMonthlyPerformanceSnapshotCommand(payload, payload.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Monthly performance snapshot could not be generated.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }
}
