using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Contracts.Reporting;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/reporting/monthly-snapshots")]
public sealed class ReportingController : ControllerBase
{
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateMonthlyPerformanceSnapshotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateAsync(
        [FromBody] GenerateMonthlyPerformanceSnapshotRequest request,
        [FromServices] GenerateMonthlyPerformanceSnapshotUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new GenerateMonthlyPerformanceSnapshotCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Monthly performance snapshot could not be generated.",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(GenerateAsync), result.Value);
    }

    [HttpPost("executive")]
    [ProducesResponseType(typeof(GenerateExecutiveReportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateExecutiveAsync(
        [FromBody] GenerateExecutiveReportRequest request,
        [FromServices] GenerateExecutiveReportUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(request, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(title: "Executive report could not be generated.", detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(GenerateExecutiveAsync), result.Value);
    }

    [HttpPost("operational")]
    [ProducesResponseType(typeof(GenerateOperationalReportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateOperationalAsync(
        [FromBody] GenerateOperationalReportRequest request,
        [FromServices] GenerateOperationalReportUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(request, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(title: "Operational report could not be generated.", detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(GenerateOperationalAsync), result.Value);
    }

    [HttpPost("recommendations")]
    [ProducesResponseType(typeof(GenerateOptimizationRecommendationsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateRecommendationsAsync(
        [FromBody] GenerateOptimizationRecommendationsRequest request,
        [FromServices] GenerateOptimizationRecommendationsUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(request, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(title: "Optimization recommendations could not be generated.", detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(GenerateRecommendationsAsync), result.Value);
    }
}
