using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Contracts.Reporting;
using AIMultiAgentPlatform.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIMultiAgentPlatform.Functions.Reporting;

public sealed class ReportingFunction(
    GenerateMonthlyPerformanceSnapshotUseCase monthlySnapshotUseCase,
    GenerateExecutiveReportUseCase executiveReportUseCase,
    GenerateOperationalReportUseCase operationalReportUseCase,
    GenerateOptimizationRecommendationsUseCase optimizationRecommendationsUseCase)
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

        var result = await monthlySnapshotUseCase.ExecuteAsync(
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

    [Function("GenerateExecutiveReport")]
    public async Task<HttpResponseData> GenerateExecutiveAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/reporting/monthly-snapshots/executive")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<GenerateExecutiveReportRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Executive report could not be generated.", "Request body is required.", cancellationToken);
        }

        var result = await executiveReportUseCase.ExecuteAsync(payload, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Executive report could not be generated.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }

    [Function("GenerateOperationalReport")]
    public async Task<HttpResponseData> GenerateOperationalAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/reporting/monthly-snapshots/operational")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<GenerateOperationalReportRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Operational report could not be generated.", "Request body is required.", cancellationToken);
        }

        var result = await operationalReportUseCase.ExecuteAsync(payload, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Operational report could not be generated.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }

    [Function("GenerateOptimizationRecommendations")]
    public async Task<HttpResponseData> GenerateRecommendationsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/reporting/monthly-snapshots/recommendations")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<GenerateOptimizationRecommendationsRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Optimization recommendations could not be generated.", "Request body is required.", cancellationToken);
        }

        var result = await optimizationRecommendationsUseCase.ExecuteAsync(payload, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Optimization recommendations could not be generated.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }
}
