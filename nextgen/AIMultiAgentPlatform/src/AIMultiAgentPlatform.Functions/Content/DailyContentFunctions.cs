using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.DailyContent;
using AIMultiAgentPlatform.Application.ReviewAndScheduling;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIMultiAgentPlatform.Functions.Content;

public sealed class DailyContentFunctions(
    GenerateDailyContentPackageUseCase generateDailyContentPackageUseCase,
    EnqueueGenerateDailyContentPackageUseCase enqueueGenerateDailyContentPackageUseCase,
    ReviewAndScheduleDailyContentUseCase reviewAndScheduleDailyContentUseCase,
    EnqueueReviewAndScheduleDailyContentUseCase enqueueReviewAndScheduleDailyContentUseCase)
{
    [Function("GenerateDailyContentPackage")]
    public async Task<HttpResponseData> GenerateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/content/daily/generate")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<GenerateDailyContentPackageRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Daily content package could not be generated.", "Request body is required.", cancellationToken);
        }

        var result = await generateDailyContentPackageUseCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(payload, payload.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Daily content package could not be generated.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }

    [Function("EnqueueGenerateDailyContentPackage")]
    public async Task<HttpResponseData> EnqueueGenerateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/content/daily/generate/enqueue")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<GenerateDailyContentPackageRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Daily content package could not be enqueued.", "Request body is required.", cancellationToken);
        }

        var result = await enqueueGenerateDailyContentPackageUseCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(payload, payload.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Daily content package could not be enqueued.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.AcceptedAsync(request, result.Value, cancellationToken);
    }

    [Function("ReviewAndScheduleDailyContent")]
    public async Task<HttpResponseData> ReviewAndScheduleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/content/daily/review-and-schedule")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<ReviewAndScheduleDailyContentRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Daily content review and scheduling could not be completed.", "Request body is required.", cancellationToken);
        }

        var result = await reviewAndScheduleDailyContentUseCase.ExecuteAsync(
            new ReviewAndScheduleDailyContentCommand(payload, payload.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Daily content review and scheduling could not be completed.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.CreatedAsync(request, result.Value, cancellationToken);
    }

    [Function("EnqueueReviewAndScheduleDailyContent")]
    public async Task<HttpResponseData> EnqueueReviewAndScheduleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/content/daily/review-and-schedule/enqueue")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await FunctionHttp.ReadJsonAsync<ReviewAndScheduleDailyContentRequest>(request, cancellationToken);
        if (payload is null)
        {
            return await FunctionHttp.BadRequestAsync(request, "Daily content review and scheduling could not be enqueued.", "Request body is required.", cancellationToken);
        }

        var result = await enqueueReviewAndScheduleDailyContentUseCase.ExecuteAsync(
            new ReviewAndScheduleDailyContentCommand(payload, payload.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return await FunctionHttp.BadRequestAsync(
                request,
                "Daily content review and scheduling could not be enqueued.",
                Result<object?>.Failure(result.ErrorCode ?? "unknown", result.ErrorMessage ?? "Unknown error."),
                cancellationToken);
        }

        return await FunctionHttp.AcceptedAsync(request, result.Value, cancellationToken);
    }
}
