using AIMultiAgentPlatform.Application.DailyContent;
using AIMultiAgentPlatform.Application.ReviewAndScheduling;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Contracts.Orchestration;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/content/daily")]
public sealed class DailyContentController : ControllerBase
{
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateDailyContentPackageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateAsync(
        [FromBody] GenerateDailyContentPackageRequest request,
        [FromServices] GenerateDailyContentPackageUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Daily content package could not be generated.",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(GenerateAsync), result.Value);
    }

    [HttpPost("generate/enqueue")]
    [ProducesResponseType(typeof(CommandEnqueueResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnqueueGenerateAsync(
        [FromBody] GenerateDailyContentPackageRequest request,
        [FromServices] EnqueueGenerateDailyContentPackageUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Daily content package could not be enqueued.",
                detail: result.ErrorMessage);
        }

        return Accepted(result.Value);
    }

    [HttpPost("review-and-schedule")]
    [ProducesResponseType(typeof(ReviewAndScheduleDailyContentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviewAndScheduleAsync(
        [FromBody] ReviewAndScheduleDailyContentRequest request,
        [FromServices] ReviewAndScheduleDailyContentUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new ReviewAndScheduleDailyContentCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Daily content review and scheduling could not be completed.",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(ReviewAndScheduleAsync), result.Value);
    }

    [HttpPost("review-and-schedule/enqueue")]
    [ProducesResponseType(typeof(CommandEnqueueResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnqueueReviewAndScheduleAsync(
        [FromBody] ReviewAndScheduleDailyContentRequest request,
        [FromServices] EnqueueReviewAndScheduleDailyContentUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new ReviewAndScheduleDailyContentCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Daily content review and scheduling could not be enqueued.",
                detail: result.ErrorMessage);
        }

        return Accepted(result.Value);
    }
}
